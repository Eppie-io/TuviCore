using System;
using System.Linq;

namespace Tuvi.Core.Backup.Impl
{
    public static class BinarySerializationExtensions
    {
        /// <summary>
        /// Big-endian integer to byte buffer serialization (most significant byte first)
        /// </summary>
        public static byte[] ToByteBuffer(this Int32 value)
        {
            var bytes = BitConverter.GetBytes(value);

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return bytes;
        }

        /// <summary>
        /// Big-endian integer from byte buffer deserialization (most significant byte first)
        /// </summary>
        public static Int32 FromByteBuffer(this byte[] bytes)
        {
            byte[] orderedBytes;

            if (BitConverter.IsLittleEndian)
            {
                orderedBytes = Enumerable.Reverse(bytes).ToArray();
            }
            else
            {
                orderedBytes = bytes;
            }

            return BitConverter.ToInt32(orderedBytes, 0);
        }
    }
}
