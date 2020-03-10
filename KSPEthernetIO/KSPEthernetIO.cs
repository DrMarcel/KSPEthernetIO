using System;
using System.Runtime.InteropServices;
using UnityEngine;
using static KSPEthernetIO.DataPackets;

namespace KSPEthernetIO
{
    /// <summary>
    /// 
    ///           *****************
    ///             KSPEthernetIO 
    ///           ***************** 
    /// 
    /// Version:     0.1.1
    /// Author:      DrMarcel
    /// 
    /// License:     CC BY 4.0 
    ///              https://creativecommons.org/licenses/by/3.0/
    /// 
    /// 
    /// Kerbal Space Program addon based on KSPSerialIO 0.19.1 by zitronen.
    /// Integrates a TCP Server in the game to communicate with external displays, controllers
    /// and other cool stuff. Be creative.
    /// 
    /// VesselData structure is slightly modified version of the original data structure from
    /// KSPSerialIO. Angles are saved as UInt16 like seen in some forks of the project.
    /// 
    /// Sample code for an Android and a RaspberryPi client is in progress.
    /// Check out the GitHub Repo and the Forum for updates.
    /// 
    /// Have fun and good flight!
    /// 
    /// 
    /// Forum link:  https://forum.kerbalspaceprogram.com/index.php?/topic/191502-ksp-181-kspethernetio-100-android-client-01-beta-ethernet-based-remote-control/
    /// GitHub repo: https://github.com/DrMarcel/KSPEthernetIO
    /// 
    /// </summary>
    class KSPEthernetIO
    {
        private static bool _initialized = false;
        public static bool Initialized
        {
            get { return _initialized; }
            set { }
        }

        public static Settings Settings = null;
        public static TcpServer Server = null;
        public static PacketHandler Handler = null;

        /// <summary>
        /// Load settings and initialize the TcpServer and PacketHandler.
        /// </summary>
        public static void Initialize()
        {
            if (!_initialized)
            {
                String version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
                Debug.Log(String.Format("[KSPEthernetIO]: Version {0}", version));
                Debug.Log(String.Format("[KSPEthernetIO]: Output packet size: {0}/{1}", Marshal.SizeOf(new VesselData()).ToString(), MaxPayloadSize));

                Settings.Load();

                Server = new TcpServer(Settings.Port);
                Server.Start();
                Handler = new PacketHandler(
                    Server,
                    Settings.WatchdogDisable, Settings.WatchdogTimeout,
                    Settings.HandshakeDisable, Settings.HandshakeTimeout,
                    Settings.BroadcastDisable, Settings.Broadcast);
                Handler.updateStatus(SNotInFlight);

                _initialized = true;
            }
        }
    }

    /// <summary>
    /// Initialize KSPEthernetIO on first Startup
    /// </summary>
    [KSPAddon(KSPAddon.Startup.MainMenu, false)]
    class Initialize : UnityEngine.MonoBehaviour
    {
        void Awake()
        {
            if (!KSPEthernetIO.Initialized) KSPEthernetIO.Initialize();
        }
    }

    /// <summary>
    /// Flight behaviour
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class Flight : MonoBehaviour, IPacketHandlerListener, IVesselDataInterfaceListener
    {
        VesselControlInterface control;
        VesselDataInterface data;

        private const int packetCounterClock = 30; //in seconds. Print received packets after this time, only for debugging
        private long packetCounter = 0; //Only for debugging
        private double lastUpdateTime = 0.0f; //Only for debugging

        /// <summary>
        /// Initialize Flight. Start listening to PacketHandler, enable ControlPackets.
        /// It doesn't matter if no controller is avaiable on flight start.
        /// The controller can also be connected in flight.
        /// </summary>
        void Awake()
        {
            lastUpdateTime = Time.unscaledTime;

            Debug.Log("[KSPEthernetIO]: Flight started"); 
            KSPEthernetIO.Handler.updateStatus(SInFlight);
            Debug.Log("[KSPEthernetIO]: Listening to PacketHandler");
            KSPEthernetIO.Handler.AddListener(this);

            Debug.Log("[KSPEthernetIO]: Initialize VesselControlInterface");
            control = new VesselControlInterface();

            Debug.Log("[KSPEthernetIO]: Initialize VesselDataInterface");
            data = new VesselDataInterface();
            data.AddListener(this);

            Debug.Log("[KSPEthernetIO]: Enable ControlPackets");
            KSPEthernetIO.Handler.EnableControlPackets(true);

            Debug.Log("[KSPEthernetIO]: Check connection");
            if (KSPEthernetIO.Server.ClientConnected)
            {
                if (KSPEthernetIO.Handler.HandshakeReceived || Settings.HandshakeDisable)
                    Debug.Log("[KSPEthernetIO]: Controller avaiable");
                else Debug.LogWarning("[KSPEthernetIO]: Handshake not finished");
            }
            else Debug.LogWarning("[KSPEthernetIO]: No controller detected!");
        }

        /// <summary>
        /// Update active vessel and if client is connected send vessel and receive control data.
        /// </summary>
        void Update()
        {
            control.UpdateActiveVessel();
            if (KSPEthernetIO.Server.ClientConnected && KSPEthernetIO.Handler.HandshakeReceived)
            {
                KSPEthernetIO.Handler.CheckForNewPacket(); //Creates events if new data is avaiable
                data.UpdateVesselData(); //Update Vesseldata if refresh time exceeds and trigger VesselDataInvalidated
            }
        }

        /// <summary>
        /// On flight end shutdown PacketHandler.
        /// </summary>
        void OnDestroy()
        {
            Debug.Log("[KSPEthernetIO]: Flight end");
            KSPEthernetIO.Handler.updateStatus(SNotInFlight);

            Debug.Log("[KSPEthernetIO]: Disable ControlPackets");
            KSPEthernetIO.Handler.EnableControlPackets(false);

            Debug.Log("[KSPEthernetIO]: Stop listening to PacketHandler");
            KSPEthernetIO.Handler.RemoveListener(this);
            data.RemoveListener(this);
        }

        /// <summary>
        /// Callback for ControlData received event
        /// </summary>
        /// <param name="CPacket">New ControlData</param>
        public void ControlPacketReceived(ControlPacket CPacket)
        {
            //Output for debugging
            packetCounter++;
            double time = Time.unscaledTime;
            double dt = time - lastUpdateTime;
            if (KSPEthernetIO.Server.ClientConnected && dt > 30)
            {
                lastUpdateTime = time;
                Debug.Log("[KSPEthernetIO]: " + packetCounter + " ControlPackets received [" + 1000 * dt / packetCounter + "ms]");
                packetCounter = 0;
            }
            if (!KSPEthernetIO.Server.ClientConnected)
            {
                lastUpdateTime = Time.unscaledTime;
                packetCounter = 0;
            }

            //Handle received packet
            control.ControlsReceived(CPacket, data.getVesselSync());
        }

        /// <summary>
        /// Callback if Refresh time was exceed and VesselData was invalidated. Send out new data.
        /// </summary>
        /// <param name="VData">New VesselData</param>
        public void VesselDataInvalidated(VesselData VData)
        {
            if (KSPEthernetIO.Server.ClientConnected && KSPEthernetIO.Handler.HandshakeReceived)
            {
                //Debug.Log("[KSPEthernetIO]: Sending VesselData...");
                KSPEthernetIO.Server.Send(StructureToPacket(VData));
            }
        }
    }
}
