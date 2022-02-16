using System;
using System.Collections.Immutable;
using System.Text;

namespace ElfTools.Chunks
{
    public record StringTableChunk : SectionChunk
    {
        /// <summary>
        /// Raw string table data.
        /// </summary>
        public ImmutableArray<char> Data { get; init; } = ImmutableArray<char>.Empty;

        public override int ByteLength => Data.Length;

        /// <summary>
        /// Returns the string beginning at the given offset and ending at the next 0 byte.
        /// </summary>
        public string GetString(uint offset)
        {
            // Find 0 byte
            int end = (int)offset;
            while(end < Data.Length && Data[end] != '\0')
                ++end;

            // Extract string
            return new string(Data.AsSpan()[(int)offset..end]);
        }

        public override int WriteTo(Span<byte> buffer)
        {
            Encoding.ASCII.GetBytes(Data.AsSpan(), buffer);

            return Data.Length;
        }

        /// <summary>
        /// Initializes the chunk from the given buffer.
        /// </summary>
        /// <param name="buffer">Buffer containing the chunk data.</param>
        /// <returns>Deserialized chunk object.</returns>
        public static StringTableChunk FromBytes(ReadOnlySpan<byte> buffer)
        {
            char[] data = new char[buffer.Length];
            Encoding.ASCII.GetChars(buffer, data);

            return new StringTableChunk
            {
                Data = data.ToImmutableArray()
            };
        }
    }
}