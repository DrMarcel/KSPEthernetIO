using System;

namespace KSPEthernetIO
{
    /// <summary>
    /// Some utility functions... 
    /// </summary>
    public class Utilities
    {
        /// <summary>
        /// Get the n-th bit of a byte.
        /// </summary>
        /// <param name="x">Byte</param>
        /// <param name="n">Selected bit (0-7)</param>
        /// <returns></returns>
        public static Boolean BitMathByte(byte x, int n)
        {
            return ((x >> n) & 1) == 1;
        }

        /// <summary>
        /// Get the n-th bit of an unsigned short.
        /// </summary>
        /// <param name="x">UShort</param>
        /// <param name="n">Selected bit (0-15)</param>
        /// <returns></returns>
        public static Boolean BitMathUshort(ushort x, int n)
        {
            return ((x >> n) & 1) == 1;
        }

        /// <summary>
        /// Scale x in range [0..360] to UInt16 range [0..65535].
        /// Also calculates x mod 360
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        public static UInt16 ToScaledUInt(double x)
        {
            if (x < 0)
            {
                x = x + 360;
            }
            else if (x > 360)
            {
                x = x % 360;
            }
            UInt16 result;
            result = (UInt16)(x * 65535 / 360);
            return result;
        }

        /// <summary>
        /// Swap x- and y-axis of a Vector.
        /// </summary>
        /// <param name="v">Source vector</param>
        /// <returns>Result vector</returns>
        public static Vector3d swapYZ(Vector3d v)
        {
            return new Vector3d(v.x, v.z, v.y);
        }

        /// <summary>
        /// Check if a target vessel exists.
        /// </summary>
        /// <returns>True if target vessel exists</returns>
        public static Boolean TargetExists()
        {
            return (FlightGlobals.fetch.VesselTarget != null) && (FlightGlobals.fetch.VesselTarget.GetVessel() != null);
        }

        /// <summary>
        /// Wrapper for angle variables
        /// </summary>
        public struct NavHeading
        {
            public float Pitch, Heading;
        }

        /// <summary>
        /// I really don't know, what this is doing...
        /// </summary>
        /// <param name="up"></param>
        /// <param name="north"></param>
        /// <param name="east"></param>
        /// <param name="v"></param>
        /// <returns></returns>
        public static NavHeading WorldVecToNavHeading(Vector3d up, Vector3d north, Vector3d east, Vector3d v)
        {
            NavHeading ret = new NavHeading();
            ret.Pitch = (float)-((Vector3d.Angle(up, v)) - 90.0f);
            Vector3d progradeFlat = Vector3d.Exclude(up, v);
            float NAngle = (float)Vector3d.Angle(north, progradeFlat);
            float EAngle = (float)Vector3d.Angle(east, progradeFlat);
            if (EAngle < 90)
                ret.Heading = NAngle;
            else
                ret.Heading = -NAngle + 360;
            return ret;
        }
    }
}
