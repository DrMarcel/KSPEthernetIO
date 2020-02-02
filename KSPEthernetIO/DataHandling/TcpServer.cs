using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using UnityEngine;
using static KSPEthernetIO.TcpServer;

namespace KSPEthernetIO
{
    /// <summary>
    /// TcpServer listening on a specific port for a connection. Is able to send data to a connected client and 
    /// to send UDP broadcast messages.
    /// Keeps itself alive after first Start() command.
    /// Provides events to ITcpServerListeners whenever a client is connected, disconnected, data is received, 
    /// sent or a broadcast was sent.
    /// </summary>
    public class TcpServer : ITcpServer
    {
        public enum TcpServerEvent { ClientConnected, ClientDisconnected, DataReceived, DataSent, BroadcastSent }

        public bool ServerStarted
        {
            get { return (_tcpListener != null); }
            set { }
        }
        public bool ClientConnected
        {
            get { return (_tcpClient != null); }
            set { }
        }

        /// <summary>
        /// Wrapper for NetworkStream and corresponding read buffer
        /// </summary>
        private class ReceiveData
        {
            public NetworkStream networkStream;
            public byte[] readBuffer;
        }

        private int _port;
        private TcpListener _tcpListener = null;
        private TcpClient _tcpClient = null;        
        private bool _clientConnectionIO
        {
            get { return (_tcpClient != null && _tcpClient.Connected && _tcpClient.Client.Connected); }
            set { }
        }

        /// <summary>
        /// Initialize new TcpServer.
        /// </summary>
        /// <param name="port">Communication port</param>
        public TcpServer(int port)
        {
            _port = port;
        }

        /// <summary>
        /// Start the TcpServer.
        /// </summary>
        public void Start()
        {
            Debug.Log("[KSPEthernetIO]: Starting TCP Server on port " + _port);
            if (!ServerStarted)
            {
                IPAddress localAddr = IPAddress.Parse("0.0.0.0");
                _tcpListener = new TcpListener(localAddr, _port);
                _tcpListener.Server.NoDelay = true;
                _tcpListener.Server.ReceiveBufferSize = 1024;
                _tcpListener.Server.SendBufferSize = 1024;
                _tcpListener.Start();

                Debug.Log("[KSPEthernetIO]: Waiting for TCP client...");
                _tcpListener.BeginAcceptTcpClient(new AsyncCallback(HandleClientConnected), _tcpListener);
            }
            else Debug.LogWarning("[KSPEthernetIO]: TcpServer already running");
        }
        /// <summary>
        /// Stop the TcpServer.
        /// </summary>
        public void Stop()
        {
            Debug.Log("[KSPEthernetIO]: Stopping TCP Server...");
            if (ServerStarted)
            {
                if (ClientConnected)
                {
                    if(_clientConnectionIO) _tcpClient.Close();
                    NotifyEvent(TcpServerEvent.ClientDisconnected);
                }
                _tcpListener.Stop();
                _tcpListener = null;
                _tcpClient = null;
                Debug.Log("[KSPEthernetIO]: TCP Server stopped");
            }
            else Debug.LogWarning("[KSPEthernetIO]: TcpServer not running");
        }
        /// <summary>
        /// Restart the TcpServer. If Server is stopped, just start it.
        /// </summary>
        public void Restart()
        {
            Stop();
            Start();
        }
       
        /// <summary>
        /// Async send data to connected client.
        /// Provides event if data was sent successfull.
        /// </summary>
        /// <param name="data">Data to send</param>
        public void Send(byte[] data)
        {
            //Debug.Log("[KSPEthernetIO]: Sending data...");
            if (!ServerStarted) Debug.LogWarning("[KSPEthernetIO]: TcpServer not started");
            else if (!ClientConnected) Debug.LogWarning("[KSPEthernetIO]: No client connected");
            else if(!_clientConnectionIO)
            {
                Debug.LogWarning("[KSPEthernetIO]: Connection lost. Restarting TcpServer...");
                Restart();
            }
            else if(!_tcpClient.GetStream().CanWrite)
            {
                Debug.LogWarning("[KSPEthernetIO]: No write permission on socket. Restarting TcpServer...");
                Restart();
            }
            else
            {
                //everthing fine, send data
                byte[] send = new byte[data.Length];
                data.CopyTo(send, 0);
                _tcpClient.GetStream().BeginWrite(send, 0, send.Length, new AsyncCallback(DataSent), _tcpClient.GetStream());
            }
        }

        /// <summary>
        /// Async send UDP broadcast. 
        /// Provides event if data was sent successfull.
        /// Causes multiple events if more than one network interface is avaiable
        /// </summary>
        /// <param name="data">Data to send</param>
        public void SendBroadcast(byte[] data)
        {
            //Loop over all avaiable Network interfaces to send broadcast over each
            //Thankfully borrowed from stackoverflow...
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus == OperationalStatus.Up && ni.SupportsMulticast && ni.GetIPProperties().GetIPv4Properties() != null)
                {
                    int id = ni.GetIPProperties().GetIPv4Properties().Index;
                    if (NetworkInterface.LoopbackInterfaceIndex != id)
                    {
                        foreach (UnicastIPAddressInformation uip in ni.GetIPProperties().UnicastAddresses)
                        {
                            if (uip.Address.AddressFamily == AddressFamily.InterNetwork)
                            {
                                IPEndPoint local = new IPEndPoint(uip.Address, 0);
                                UdpClient udpc = new UdpClient(local);
                                udpc.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
                                udpc.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontRoute, 1);
                                IPEndPoint target = new IPEndPoint(IPAddress.Broadcast, _port);
                                udpc.BeginSend(data, data.Length, target, new AsyncCallback(BroadcastSent), udpc);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Callback for client connection request.
        /// </summary>
        /// <param name="result"></param>
        private void HandleClientConnected(IAsyncResult result)
        {
            Debug.Log("[KSPEthernetIO]: Received client request.");
            TcpListener tcpListener = (TcpListener)result.AsyncState;
            _tcpClient = tcpListener.EndAcceptTcpClient(result);
            IPEndPoint clientEndpoint = (IPEndPoint)_tcpClient.Client.RemoteEndPoint;
            Debug.Log("[KSPEthernetIO]: Connection to client " + clientEndpoint.Address+":"+clientEndpoint.Port+" established.");

            //Async wait for data
            WaitForData(_tcpClient.GetStream());

            //Notify listeners
            NotifyEvent(TcpServerEvent.ClientConnected);
        }
        
        /// <summary>
        /// Async wait for incoming data on NetworkStream
        /// </summary>
        /// <param name="stream">NetworkStream</param>
        private void WaitForData(NetworkStream stream)
        {
            if (!ServerStarted || !ClientConnected || !_clientConnectionIO || !stream.CanRead)
            {
                Debug.LogWarning("[KSPEthernetIO]: Data receive error. Restarting TCP Server...");
                Restart();
            }
            else
            {
                //Create data wrapper to get buffer and corresponding NetworkStream in callback
                ReceiveData data = new ReceiveData();
                data.readBuffer = new byte[1024];
                data.networkStream = stream;
                data.networkStream.BeginRead(data.readBuffer, 0, data.readBuffer.Length, new AsyncCallback(DataReceived), data);
            }
        }
       
        /// <summary>
        /// Callback for data received
        /// </summary>
        /// <param name="result"></param>
        private void DataReceived(IAsyncResult result)
        {
            ReceiveData data = (ReceiveData)result.AsyncState;
            int n = data.networkStream.EndRead(result);

            //FIN packet creates data received event with 0 bytes
            if (n > 0)
            {
                byte[] received = new byte[n];
                char[] cstr = new char[n+1];
                for (int i = 0; i < n; i++)
                {
                    received[i] = data.readBuffer[i];
                    cstr[i] = (char)data.readBuffer[i];
                }
                cstr[n] = '\0';
                //Debug.Log("[KSPEthernetIO]: " + n + " bytes received:\n" + new String(cstr));

                //Async wait for next data
                WaitForData(data.networkStream);

                //Notify listeners
                NotifyDataReceived(received);
            }
            else
            {
                Debug.LogWarning("[KSPEthernetIO]: Connection closed. Restarting TCP Server!");
                Restart();
            }
        }
        
        /// <summary>
        /// Callback for data sent.
        /// </summary>
        /// <param name="result"></param>
        private void DataSent(IAsyncResult result)
        {
            NetworkStream stream = (NetworkStream)result.AsyncState;
            stream.EndWrite(result);
            //Debug.Log("[KSPEthernetIO]: Data sent");

            //Notify listeners
            NotifyEvent(TcpServerEvent.DataSent);
        }
        
        /// <summary>
        /// Callback for broadcast sent
        /// </summary>
        /// <param name="result"></param>
        private void BroadcastSent(IAsyncResult result)
        {
            UdpClient sender = (UdpClient)result.AsyncState;
            sender.EndSend(result);

            //Debug.Log("[KSPEthernetIO]: Broadcast sent");

            //Notify listeners
            NotifyEvent(TcpServerEvent.BroadcastSent);
        }
    
    }

    /// <summary>
    /// TcpServer listener interface. For possible events see TcpServer.TcpServerEvent.
    /// </summary>
    public interface ITcpServerListener
    {
        void TcpServerEvent(TcpServerEvent serverEvent, byte[] data);
    }

    /// <summary>
    /// TcpServer event provider.
    /// </summary>
    public abstract class ITcpServer
    {
        private ArrayList listeners = new ArrayList();

        public void AddListener(ITcpServerListener l)
        {
            listeners.Add(l);
        }

        public void RemoveListener(ITcpServerListener l)
        {
            listeners.Remove(l);
        }

        protected void NotifyEvent(TcpServerEvent serverEvent)
        {
            foreach (ITcpServerListener l in listeners) l.TcpServerEvent(serverEvent, null);
        }
        protected void NotifyDataReceived(byte[] data)
        {
            foreach (ITcpServerListener l in listeners) l.TcpServerEvent(TcpServerEvent.DataReceived, data);
        }
    }


}
