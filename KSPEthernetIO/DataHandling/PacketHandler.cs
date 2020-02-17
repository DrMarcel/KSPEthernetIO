using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using static KSPEthernetIO.DataPackets;

namespace KSPEthernetIO
{
    /// <summary>
    /// PacketHandler is connected to TcpClient. Provides events whenever a ControlPacket was received.
    /// The events can be recived by an IPacketHandlerListener.
    /// Automaticly checks for valid handshake after connection.
    /// Optional Watchdog timer to reset connection if connection is lost.
    /// </summary>
    public class PacketHandler : IPacketHandler, ITcpServerListener
    {
        private TcpServer _server;

        private Mutex _dataReceiveMutex = new Mutex(); //Guards access to data shared between threads
        private bool _handshakeReceived = false; //Indicates if a handshake was received in the current session
        private bool _enableControl = false; //Enable/Disable receive of ControlPackets
        private Stack<ControlPacket> _cPacketStack = new Stack<ControlPacket>(); //Receive packet stack

        private Mutex _watchdogMutex = new Mutex(); //Guards access to data shared between threads
        private bool _watchdogDisable;
        private int _watchdogTimer = 0;
        private int _watchdogTimeout;

        private bool _broadcastDisable;
        private int _broadcastTime;

        private bool _handshakeDisable;
        private int _handshakeTimeout;

        public bool HandshakeReceived //Thread safe
        {
            get
            {
                _dataReceiveMutex.WaitOne();
                bool tmp = _handshakeReceived || _handshakeDisable;
                _dataReceiveMutex.ReleaseMutex();
                return tmp;
            }
            set { }
        }

        private StatusPacket _status = new StatusPacket();

        private enum ReceiveStates : byte
        {
            FIRSTHEADER,  // Waiting for first header
            SECONDHEADER, // Waiting for second header
            SIZE,         // Waiting for payload size
            PAYLOAD,      // Waiting for rest of payload
            CS            // Waiting for checksum
        }

        /// <summary>
        /// Initialize new PacketHander for TcpServer.
        /// The TcpServer has to be initialized, but has not to be started.
        /// </summary>
        /// <param name="server">TcpServer</param>
        /// <param name="disableWatchdog">Disable watchdog timer</param>
        /// <param name="watchdogTimeout">Watchdog timeout in milliseconds</param>
        /// <param name="disableHandshake">Disable handshake check</param>
        /// <param name="handshakeTimeout">Handshake timeout in milliseconds</param>
        /// <param name="disableBroadcast">Disable broadcasting</param>
        /// <param name="broadcastTime">Broadcast clock in milliseconds</param>
        public PacketHandler(TcpServer server, bool disableWatchdog, int watchdogTimeout, bool disableHandshake, int handshakeTimeout, bool disableBroadcast, int broadcastTime)
        {
            _watchdogDisable = disableWatchdog;
            _broadcastTime = broadcastTime;
            _broadcastDisable = disableBroadcast;
            _watchdogTimeout = watchdogTimeout;
            _handshakeDisable = disableHandshake;
            _handshakeTimeout = handshakeTimeout;

            _server = server;
            _server.AddListener(this);

            _status.id = SPid;
            _status.status = SUndefined;

            Debug.Log("[KSPEthernetIO]: Initialize PacketHandler");

            if (_handshakeDisable) Debug.Log("[KSPEthernetIO]: Handshake disabled");
            else Debug.Log("[KSPEthernetIO]: Handshake enabled");

            if (_broadcastDisable) Debug.Log("[KSPEthernetIO]: Broadcast disabled");
            else Debug.Log("[KSPEthernetIO]: Broadcast enabled");

            Debug.Log("[KSPEthernetIO]: Starting background worker");
            Thread t = new Thread(new ThreadStart(ThreadBackgroundWorker));
            t.Start();

            Debug.Log("[KSPEthernetIO]: PacketHandler ready");
        }

        /// <summary>
        /// Event handler for TcpServer events.
        /// </summary>
        /// <param name="serverEvent">Event type</param>
        /// <param name="data">Data for DataReceived events</param>
        public void TcpServerEvent(TcpServer.TcpServerEvent serverEvent, byte[] data)
        {
            switch(serverEvent)
            {
                case TcpServer.TcpServerEvent.ClientConnected:
                    break;
                case TcpServer.TcpServerEvent.ClientDisconnected:
                    _dataReceiveMutex.WaitOne();
                    _handshakeReceived = false;
                    _dataReceiveMutex.ReleaseMutex();
                    break;
                case TcpServer.TcpServerEvent.DataReceived:
                    ResetWatchdog(); //Reset the WDT every time data is received
                    ReceivedDataEvent(data);
                    break;
                case TcpServer.TcpServerEvent.DataSent:
                    break;
                case TcpServer.TcpServerEvent.BroadcastSent:
                    //Debug.Log("[KSPEthernetIO]: Handshake broadcast sent");
                    break;
            }
        }

        /// <summary>
        /// Get packets out of the received data. Check length, checksum and header.
        /// Modified version of the original code from KSPSerialIO.
        /// The original code made sure, that it's possible to receive the data in packs
        /// smaller than one full frame. The Tcp Server receives the data in whole frames,
        /// in worst case multiple frames are buffered. So CurrentState has not to be saved
        /// between multiple calls.
        /// </summary>
        /// <param name="data">Received data</param>
        private void ReceivedDataEvent(byte[] data)
        {

            ReceiveStates CurrentState = ReceiveStates.FIRSTHEADER;
            byte CurrentPacketLength=0;
            byte CurrentBytesRead=0;
            byte[] PayloadBuffer = new byte[DataPackets.MaxPayloadSize];
            byte[] NewPacketBuffer = new byte[DataPackets.MaxPayloadSize];

            for (int x = 0; x < data.Length; x++)
            {
                switch (CurrentState)
                {
                    case ReceiveStates.FIRSTHEADER:
                        if (data[x] == 0xBE) CurrentState = ReceiveStates.SECONDHEADER;
                        break;
                    case ReceiveStates.SECONDHEADER:
                        if (data[x] == 0xEF) CurrentState = ReceiveStates.SIZE;
                        else CurrentState = ReceiveStates.FIRSTHEADER;
                        break;
                    case ReceiveStates.SIZE:
                        CurrentPacketLength = data[x];
                        CurrentBytesRead = 0;
                        CurrentState = ReceiveStates.PAYLOAD;
                        break;
                    case ReceiveStates.PAYLOAD:
                        PayloadBuffer[CurrentBytesRead] = data[x];
                        CurrentBytesRead++;
                        if (CurrentBytesRead == CurrentPacketLength)
                        {
                            CurrentState = ReceiveStates.CS;
                        }
                        break;
                    case ReceiveStates.CS:
                        if (CompareChecksum(data[x], CurrentPacketLength, PayloadBuffer))
                        {
                            Buffer.BlockCopy(PayloadBuffer, 0, NewPacketBuffer, 0, CurrentBytesRead);
                            InboundPacketHandler(NewPacketBuffer);
                        }
                        else Debug.LogWarning("[KSPEthernetIO]: Checksum error");
                        CurrentState = ReceiveStates.FIRSTHEADER;
                        break;
                }
            }
        }

        /// <summary>
        /// Helper for ReceivedDataEvent. Checks the checksum of given data.
        /// </summary>
        /// <param name="readCS">Checksum read from packet</param>
        /// <param name="CurrentPacketLength">Packet length</param>
        /// <param name="PayloadBuffer">Payload</param>
        /// <returns>True if checksum is correct</returns>
        private Boolean CompareChecksum(byte readCS, byte CurrentPacketLength, byte[] PayloadBuffer)
        {
            byte calcCS = CurrentPacketLength;
            for (int i = 0; i < CurrentPacketLength; i++) calcCS ^= PayloadBuffer[i];
            return (calcCS == readCS);
        }

        /// <summary>
        /// Handle received packet.
        /// </summary>
        /// <param name="packet">Packet data starting with packet id</param>
        private void InboundPacketHandler(byte[] packet)
        {
            switch (packet[0])
            {
                //Save if correct HS was received
                case HSPid:
                    //Debug.Log("[KSPEthernetIO]: HandshakePacket received");
                    HandshakePacket HPacket = new HandshakePacket();
                    HPacket = (HandshakePacket)ByteArrayToStructure(packet, HPacket);
                    _dataReceiveMutex.WaitOne();
                    //Check hard coded HS values
                    if ((HPacket.M1 == 3) && (HPacket.M2 == 1) && (HPacket.status == 4))
                    {
                        Debug.Log("[KSPEthernetIO]: Handshake complete");
                        _handshakeReceived = true;
                    }
                    else _handshakeReceived = false; 
                    _dataReceiveMutex.ReleaseMutex();
                    break;

                //Stack received ControlPackets
                case Cid:
                    //Debug.Log("[KSPEthernetIO]: ControlPacket received");
                    _dataReceiveMutex.WaitOne();
                    if (_cPacketStack.Count < 256 && _enableControl)
                    {
                        ControlPacket cPacket = new ControlPacket();
                        cPacket = (ControlPacket)ByteArrayToStructure(packet, cPacket);
                        _cPacketStack.Push(cPacket);
                        if (_cPacketStack.Count >= 256) Debug.LogWarning("[KSPEthernetIO]: ControlPacket buffer overflow!");
                    }
                    _dataReceiveMutex.ReleaseMutex();
                    break;

                default:
                    Debug.LogWarning("[KSPEthernetIO]: Packet ID " + packet[0] + " unknown");
                    break;
            }

        }

        /// <summary>
        /// Has to be called in the Update() routine. Checks for new Packets in the PacketStack
        /// and creates an Event if new data is avaiable. Event can be received from the PacketHandler
        /// by IPacketHandlerListeners.
        /// </summary>
        public void CheckForNewPacket()
        {
            ControlPacket cPacket = new ControlPacket();
            bool newcpacket = false;

            _dataReceiveMutex.WaitOne();

            if (_cPacketStack.Count > 0)
            {
                newcpacket = true;
                cPacket = _cPacketStack.Pop();
            }

            _dataReceiveMutex.ReleaseMutex();

            if (newcpacket) NotifyCPacketReceived(cPacket);
        }

        /// <summary>
        /// Enable / Disable the receive of ControlPackets.
        /// ControlPackets should be enabled during flight only to prevent PacketStack overflow.
        /// </summary>
        /// <param name="en">Enable</param>
        public void EnableControlPackets(Boolean en)
        {
            _dataReceiveMutex.WaitOne();

            _enableControl = en;
            if (!en) _cPacketStack.Clear();

            _dataReceiveMutex.ReleaseMutex();
        }

        /// <summary>
        /// Reset Watchdogtimer.
        /// </summary>
        private void ResetWatchdog()
        {
            _watchdogMutex.WaitOne();
            _watchdogTimer = 0;
            _watchdogMutex.ReleaseMutex();
        }
        /// <summary>
        /// Check if maximum Watchdog time is exceed.
        /// </summary>
        /// <param name="ms">Maximum time in milliseconds</param>
        /// <returns>True if exceed</returns>
        private bool WatchdogExceed(int ms)
        {
            bool b = false;
            _watchdogMutex.WaitOne();
            if (_watchdogTimer >= ms) b = true;
            _watchdogMutex.ReleaseMutex();
            return b;
        }
        /// <summary>
        /// Increase Watchdog timer.
        /// </summary>
        /// <param name="ms">Milliseconds since last call</param>
        private void WatchdogTick(int ms)
        {
            _watchdogMutex.WaitOne();
            _watchdogTimer += ms;
            _watchdogMutex.ReleaseMutex();
        }

        /// <summary>
        /// Background worker thread, always active.
        /// Checks for handshake and watchdog errors and triggers send of broadcast
        /// messages while no client is connected.
        /// </summary>
        private void ThreadBackgroundWorker()
        {
            const int dt = 10; // Worker tick in milliseconds

            //Hardcoded HS Packet for broadcasts
            HandshakePacket HPacket = new HandshakePacket();
            HPacket.id = HSPid;
            HPacket.M1 = 1; HPacket.M2 = 2; HPacket.status = _status.status;

            int broadcastTimer = 0;
            int handshakeTimer = 0;

            ResetWatchdog();
            
            while (true)
            {
                //If not connected send broadcasts
                if (!_server.ClientConnected && !_broadcastDisable)
                {
                    broadcastTimer += dt;
                    if (broadcastTimer >= _broadcastTime)
                    {
                        broadcastTimer = 0;
                        //Debug.Log("[KSPEthernetIO]: Sending broadcast...");
                        HPacket.status = _status.status;
                        _server.SendBroadcast(StructureToPacket(HPacket));
                    }
                }
                else broadcastTimer = 0;

                //If client connected wait for handshake
                if (_server.ClientConnected && !HandshakeReceived && !_handshakeDisable)
                {
                    handshakeTimer += dt;
                    if (handshakeTimer >= _handshakeTimeout)
                    {
                        handshakeTimer = 0;
                        Debug.LogWarning("[KSPEthernetIO]: Handshake was not successful");
                        _server.Restart();
                    }
                }
                else handshakeTimer = 0;

                //Watchdog
                if (_server.ClientConnected && (HandshakeReceived || _handshakeDisable))
                {
                    WatchdogTick(dt);
                    if (WatchdogExceed(_watchdogTimeout))
                    {
                        Debug.LogWarning("[KSPEthernetIO]: Watchdog time exceed");
                        _server.Restart();
                    }
                }
                else ResetWatchdog();

                Thread.Sleep(dt);
                
            }
        }

        /// <summary>
        /// Send current host state to client
        /// </summary>
        /// <param name="status">Host state</param>
        public void updateStatus(byte status)
        {
            if (_status.status != status)
            {
                _status.status = status;
                sendStatus();
            }
        }

        /// <summary>
        /// Helper function to send StatusPacket
        /// </summary>
        private void sendStatus()
        {
            if (_server.ClientConnected && (HandshakeReceived || _handshakeDisable))
                _server.Send(StructureToPacket(_status));
        }
    }

    /// <summary>
    /// Listener interface for PacketHandler.
    /// </summary>
    public interface IPacketHandlerListener
    {
        void ControlPacketReceived(ControlPacket CPacket);
    }

    /// <summary>
    /// PacketHandler event provider.
    /// </summary>
    public abstract class IPacketHandler
    {
        private ArrayList listeners = new ArrayList();

        public void AddListener(IPacketHandlerListener l)
        {
            listeners.Add(l);
        }

        public void RemoveListener(IPacketHandlerListener l)
        {
            listeners.Remove(l);
        }

        protected void NotifyCPacketReceived(ControlPacket CPacket)
        {
            foreach (IPacketHandlerListener l in listeners) l.ControlPacketReceived(CPacket);
        }
    }

}
