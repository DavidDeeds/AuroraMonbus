using System;
using System.Buffers.Binary;

namespace Aurora.Protocol
{
    /// <summary>
    /// Provides decoding helpers for Aurora inverter protocol frames.
    /// ABB / Power-One / FIMER devices transmit all multi-byte fields
    /// as big-endian IEEE 754 floats or unsigned integers.
    /// </summary>
    public static class ByteArrayExtensions
    {
        /// <summary>
        /// Reads a 16-bit unsigned integer (big-endian) from a byte array.
        /// </summary>
        public static ushort AsUInt16(this byte[] data, int offset = 2)
        {
            if (data == null || data.Length < offset + 2)
                return 0;

            return BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset, 2));
        }

        /// <summary>
        /// Reads a 16-bit signed integer (big-endian) from a byte array.
        /// </summary>
        public static short AsInt16BigEndian(this byte[] data, int offset = 2)
        {
            if (data == null || data.Length < offset + 2)
                return 0;

            return BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(offset, 2));
        }

        /// <summary>
        /// Reads a 32-bit unsigned integer (big-endian) from a byte array.
        /// Commonly used for CE registers (energy counters).
        /// </summary>
        public static uint AsUInt32(this byte[] data, int offset = 2)
        {
            if (data == null || data.Length < offset + 4)
                return 0;

            return BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, 4));
        }

        /// <summary>
        /// Reads a 32-bit IEEE 754 float (big-endian) from a byte array.
        /// This matches the inverter’s “measure” register format (B2..B5).
        /// </summary>
        public static float AsFloatBigEndian(this byte[] data, int offset = 2)
        {
            if (data == null || data.Length < offset + 4)
                return float.NaN;

            // ABB Aurora sends floats as big-endian words; BitConverter expects little-endian.
            uint value = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, 4));
            return BitConverter.Int32BitsToSingle((int)value);
        }

        /// <summary>
        /// Utility for debugging raw bytes in human-readable hex (00-FF-...).
        /// </summary>
        public static string ToHexString(this byte[] data, int count = -1)
        {
            if (data == null) return string.Empty;
            int len = (count > 0 && count < data.Length) ? count : data.Length;
            return BitConverter.ToString(data, 0, len);
        }
    }
}
