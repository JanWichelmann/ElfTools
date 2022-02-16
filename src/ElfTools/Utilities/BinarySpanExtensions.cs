using System;
using System.Buffers.Binary;

namespace ElfTools.Utilities
{
    internal static class BinarySpanExtensions
    {
        public static byte[] ReadBytes(this ReadOnlySpan<byte> span, ref int offset, int length)
        {
            var result = new byte[length];

            for(int i = 0; i < length; ++i)
                result[i] = span[offset + i];

            offset += length;
            return result;
        }

        public static byte ReadByte(this ReadOnlySpan<byte> span, ref int offset)
        {
            var result = span[offset];
            offset += 1;
            return result;
        }

        public static ushort ReadUInt16(this ReadOnlySpan<byte> span, ref int offset)
        {
            var result = BinaryPrimitives.ReadUInt16LittleEndian(span[offset..]);
            offset += 2;
            return result;
        }

        public static uint ReadUInt32(this ReadOnlySpan<byte> span, ref int offset)
        {
            var result = BinaryPrimitives.ReadUInt32LittleEndian(span[offset..]);
            offset += 4;
            return result;
        }

        public static long ReadInt64(this ReadOnlySpan<byte> span, ref int offset)
        {
            var result = BinaryPrimitives.ReadInt64LittleEndian(span[offset..]);
            offset += 8;
            return result;
        }

        public static ulong ReadUInt64(this ReadOnlySpan<byte> span, ref int offset)
        {
            var result = BinaryPrimitives.ReadUInt64LittleEndian(span[offset..]);
            offset += 8;
            return result;
        }

        public static void WriteBytes(this Span<byte> span, ReadOnlySpan<byte> data, ref int offset)
        {
            for(int i = 0; i < data.Length; ++i)
                span[offset + i] = data[i];

            offset += data.Length;
        }

        public static void WriteByte(this Span<byte> span, byte value, ref int offset)
        {
            span[offset] = value;
            offset += 1;
        }

        public static void WriteUInt16(this Span<byte> span, ushort value, ref int offset)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(span[offset..], value);
            offset += 2;
        }

        public static void WriteUInt32(this Span<byte> span, uint value, ref int offset)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(span[offset..], value);
            offset += 4;
        }

        public static void WriteInt64(this Span<byte> span, long value, ref int offset)
        {
            BinaryPrimitives.WriteInt64LittleEndian(span[offset..], value);
            offset += 8;
        }

        public static void WriteUInt64(this Span<byte> span, ulong value, ref int offset)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(span[offset..], value);
            offset += 8;
        }
    }
}