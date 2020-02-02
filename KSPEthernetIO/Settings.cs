using KSP.IO;
using UnityEngine;

namespace KSPEthernetIO
{
    /// <summary>
    /// Mainly identical with zitronen KSPEthernetIO 0.19.1.
    /// Only replaced Serial stuff with TcpClient stuff.
    /// </summary>
    class Settings
    {
        private const bool disableOutput = false;

        private static PluginConfiguration cfg = PluginConfiguration.CreateForType<Settings>();

        public static int Port;
        public static int Refresh;

        public static bool BroadcastDisable;
        public static int Broadcast;

        public static bool WatchdogDisable;
        public static int WatchdogTimeout;

        public static bool HandshakeDisable;
        public static int HandshakeTimeout;

        // Throttle and axis controls have the following settings:
        // 0: The internal value (supplied by KSP) is always used.
        // 1: The external value (read from serial packet) is always used.
        // 2: If the internal value is not zero use it, otherwise use the external value.
        // 3: If the external value is not zero use it, otherwise use the internal value.    
        
        public static int PitchEnable;
        public static int RollEnable;
        public static int YawEnable;
        public static int TXEnable;
        public static int TYEnable;
        public static int TZEnable;
        public static int WheelSteerEnable;
        public static int ThrottleEnable;
        public static int WheelThrottleEnable;

        public static double SASTol;

        /// <summary>
        /// Load settings.
        /// </summary>
        public static void Load()
        {
            print("[KSPEthernetIO]: Loading settings...");

            cfg.load();

            Port = cfg.GetValue<int>("Port");
            print("[KSPEthernetIO]: Port = " + Port);

            Refresh = cfg.GetValue<int>("Refresh");
            print("[KSPEthernetIO]: Refresh = " + Refresh.ToString());

            BroadcastDisable = (cfg.GetValue<int>("BroadcastDisable") == 0) ? false : true;
            print("[KSPEthernetIO]: BroadcastDisable = " + BroadcastDisable.ToString());

            Broadcast = cfg.GetValue<int>("Broadcast");
            print("[KSPEthernetIO]: Broadcast = " + Broadcast.ToString());

            WatchdogDisable = (cfg.GetValue<int>("WatchdogDisable") == 0) ? false : true;
            print("[KSPEthernetIO]: WatchdogDisable = " + WatchdogDisable.ToString());

            WatchdogTimeout = cfg.GetValue<int>("WatchdogTimeout");
            print("[KSPEthernetIO]: WatchdogTimeout = " + WatchdogTimeout.ToString());

            HandshakeDisable = (cfg.GetValue<int>("HandshakeDisable") == 0) ? false : true;
            print("[KSPEthernetIO]: HandshakeDisable = " + HandshakeDisable.ToString());

            HandshakeTimeout = cfg.GetValue<int>("HandshakeTimeout");
            print("[KSPEthernetIO]: HandshakeTimeout = " + HandshakeTimeout.ToString());

            PitchEnable = cfg.GetValue<int>("PitchEnable");
            print("[KSPEthernetIO]: PitchEnable = " + PitchEnable.ToString());

            RollEnable = cfg.GetValue<int>("RollEnable");
            print("[KSPEthernetIO]: RollEnable = " + RollEnable.ToString());

            YawEnable = cfg.GetValue<int>("YawEnable");
            print("[KSPEthernetIO]: YawEnable = " + YawEnable.ToString());

            TXEnable = cfg.GetValue<int>("TXEnable");
            print("[KSPEthernetIO]: Translate X Enable = " + TXEnable.ToString());

            TYEnable = cfg.GetValue<int>("TYEnable");
            print("[KSPEthernetIO]: Translate Y Enable = " + TYEnable.ToString());

            TZEnable = cfg.GetValue<int>("TZEnable");
            print("[KSPEthernetIO]: Translate Z Enable = " + TZEnable.ToString());

            WheelSteerEnable = cfg.GetValue<int>("WheelSteerEnable");
            print("[KSPEthernetIO]: Wheel Steering Enable = " + WheelSteerEnable.ToString());

            ThrottleEnable = cfg.GetValue<int>("ThrottleEnable");
            print("[KSPEthernetIO]: Throttle Enable = " + ThrottleEnable.ToString());

            WheelThrottleEnable = cfg.GetValue<int>("WheelThrottleEnable");
            print("[KSPEthernetIO]: Wheel Throttle Enable = " + WheelThrottleEnable.ToString());

            SASTol = cfg.GetValue<double>("SASTol");
            print("[KSPEthernetIO]: SAS Tol = " + SASTol.ToString());
        }

        private static void print(string v)
        {
            if(!disableOutput)
            {
                Debug.Log(v);
            }
        }
    }
}
