using System;
using System.Runtime.InteropServices;

namespace KSPEthernetIO
{
    /// <summary>
    /// Contains data packet structures and functions to convert between structs and byte arrays.
    /// Slighly modified version of zitronen-git/KSPSerialIO 0.19.1
    /// Angles changed to UInt16 like in some other forks of the original code.
    /// </summary>
    public class DataPackets
    {
        public const int MaxPayloadSize = 255;
        public const byte HSPid = 0, VDid = 1, Cid = 101; //hard coded values for packet IDs

        /// <summary>
        /// Unidirectional data from KSPEthernetIO to client.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct VesselData
        {
            public byte id;              //1
            public float AP;             //2
            public float PE;             //3
            public float SemiMajorAxis;  //4
            public float SemiMinorAxis;  //5
            public float VVI;            //6
            public float e;              //7
            public float inc;            //8
            public float G;              //9
            public int TAp;              //10
            public int TPe;              //11
            public float TrueAnomaly;    //12
            public float Density;        //13
            public int period;           //14
            public float RAlt;           //15
            public float Alt;            //16
            public float Vsurf;          //17
            public float Lat;            //18
            public float Lon;            //19
            public float LiquidFuelTot;  //20
            public float LiquidFuel;     //21
            public float OxidizerTot;    //22
            public float Oxidizer;       //23
            public float EChargeTot;     //24
            public float ECharge;        //25
            public float MonoPropTot;    //26
            public float MonoProp;       //27
            public float IntakeAirTot;   //28
            public float IntakeAir;      //29
            public float SolidFuelTot;   //30
            public float SolidFuel;      //31
            public float XenonGasTot;    //32
            public float XenonGas;       //33
            public float LiquidFuelTotS; //34
            public float LiquidFuelS;    //35
            public float OxidizerTotS;   //36
            public float OxidizerS;      //37
            public UInt32 MissionTime;   //38
            public float deltaTime;      //39
            public float VOrbit;         //40
            public UInt32 MNTime;        //41
            public float MNDeltaV;       //42
            public UInt16 Pitch;          //43
            public UInt16 Roll;           //44
            public UInt16 Heading;        //45
            public UInt16 ActionGroups;  //46  status bit order:SAS, RCS, Light, Gear, Brakes, Abort, Custom01 - 10 
            public byte SOINumber;       //47  SOI Number (decimal format: sun-planet-moon e.g. 130 = kerbin, 131 = mun)
            public byte MaxOverHeat;     //48  Max part overheat (% percent)
            public float MachNumber;     //49
            public float IAS;            //50  Indicated Air Speed
            public byte CurrentStage;    //51  Current stage number
            public byte TotalStage;      //52  TotalNumber of stages
            public float TargetDist;     //53  Distance to targeted vessel (m)
            public float TargetV;        //54  Target vessel relative velocity (m/s)
            public byte NavballSASMode;  //55  Combined byte for navball target mode and SAS mode
                                         // First four bits indicate AutoPilot mode:
                                         // 0 SAS is off  //1 = Regular Stability Assist //2 = Prograde
                                         // 3 = RetroGrade //4 = Normal //5 = Antinormal //6 = Radial In
                                         // 7 = Radial Out //8 = Target //9 = Anti-Target //10 = Maneuver node
                                         // Last 4 bits set navball mode. (0=ignore,1=ORBIT,2=SURFACE,3=TARGET)
            public UInt16 ProgradePitch;  //56 Pitch   Of the Prograde Vector;  int_16 ranging from (-0x8000(-360 degrees) to 0x7FFF(359.99ish degrees)); 
            public UInt16 ProgradeHeading;//57 Heading Of the Prograde Vector;  see above for range   (Prograde vector depends on navball mode, eg Surface/Orbit/Target)
            public UInt16 ManeuverPitch;  //58 Pitch   Of the Maneuver Vector;  see above for range;  (0 if no Maneuver node)
            public UInt16 ManeuverHeading;//59 Heading Of the Maneuver Vector;  see above for range;  (0 if no Maneuver node)
            public UInt16 TargetPitch;    //60 Pitch   Of the Target   Vector;  see above for range;  (0 if no Target)
            public UInt16 TargetHeading;  //61 Heading Of the Target   Vector;  see above for range;  (0 if no Target)
            public UInt16 NormalHeading;  //62 Heading Of the Prograde Vector;  see above for range;  (Pitch of the Heading Vector is always 0)
        }

        /// <summary>
        /// Bidirectional handshake data.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct HandshakePacket
        {
            public byte id;
            public byte M1;
            public byte M2;
            public byte M3;
        }

        /// <summary>
        /// Unidirectional data from client to KSPEthernetIO.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct ControlPacket
        {
            public byte id;
            public byte MainControls;                  //SAS RCS Lights Gear Brakes Precision Abort Stage 
            public byte Mode;                          //0 = stage, 1 = docking, 2 = map (Bit 0-3) 
                                                       //0 = Auto, 1 = Free, 2 = Orbital, 3 = Chase, 4 = Locked (Bit 4-7)
            public ushort ControlGroup;                //control groups 1-10 in 2 bytes
            public byte NavballSASMode;                //AutoPilot mode (See above for AutoPilot modes)(Ignored if the equal to zero or out of bounds (>10)) //Navball mode
            public byte AdditionalControlByte1;        //Bit 0: Open Menu, Bit 1: Open Map
            public short Pitch;                        //-1000 -> 1000
            public short Roll;                         //-1000 -> 1000
            public short Yaw;                          //-1000 -> 1000
            public short TX;                           //-1000 -> 1000
            public short TY;                           //-1000 -> 1000
            public short TZ;                           //-1000 -> 1000
            public short WheelSteer;                   //-1000 -> 1000
            public short Throttle;                     // 0 -> 1000
            public short WheelThrottle;                // 0 -> 1000
        };

        /// <summary>
        /// Creates a data packet from any given object.
        /// Packet structure:
        /// Packet = [header][size][payload][checksum];
        /// Header = [Header1=0xBE][Header2=0xEF]
        /// size   = [payload.length (0-255)]
        /// </summary>
        /// <param name="anything">Payload</param>
        /// <returns>Packet</returns>
        public static byte[] StructureToPacket(object anything)
        {
            byte[] Payload = StructureToByteArray(anything);
            byte header1 = 0xBE;
            byte header2 = 0xEF;
            byte size = (byte)Payload.Length;
            byte checksum = size;

            byte[] Packet = new byte[size + 4];

            for (int i = 0; i < size; i++)
            {
                checksum ^= Payload[i];
            }

            Payload.CopyTo(Packet, 3);
            Packet[0] = header1;
            Packet[1] = header2;
            Packet[2] = size;
            Packet[Packet.Length - 1] = checksum;

            return Packet;
        }

        /// <summary>
        /// Helper for StructureToPacket.
        /// Copied from the intarwebs, converts struct to byte array.
        /// </summary>
        /// <param name="obj">Object to convert</param>
        /// <returns>Byte array</returns>
        public static byte[] StructureToByteArray(object obj)
        {
            int len = Marshal.SizeOf(obj);
            byte[] arr = new byte[len];
            IntPtr ptr = Marshal.AllocHGlobal(len);
            Marshal.StructureToPtr(obj, ptr, true);
            Marshal.Copy(ptr, arr, 0, len);
            Marshal.FreeHGlobal(ptr);
            return arr;
        }

        /// <summary>
        /// Get a Structure back from a bytearray.
        /// Example call: 
        /// HPacket = (HandshakePacket) ByteArrayToStructure(packet, HPacket);
        /// </summary>
        /// <param name="bytearray">Bytearray</param>
        /// <param name="obj">Target Structure</param>
        /// <returns>Structure</returns>
        public static object ByteArrayToStructure(byte[] bytearray, object obj)
        {
            int len = Marshal.SizeOf(obj);
            IntPtr i = Marshal.AllocHGlobal(len);
            Marshal.Copy(bytearray, 0, i, len);
            obj = Marshal.PtrToStructure(i, obj.GetType());
            Marshal.FreeHGlobal(i);
            return obj;
        }
    }
}
