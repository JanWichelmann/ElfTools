using System;
using System.Collections.Immutable;
using System.Linq;

namespace ElfTools.Chunks
{
    /// <summary>
    /// Represents a raw section which was not recognized by the ELF reader.
    /// </summary>
    public record RawSectionChunk : SectionChunk
    {
        /// <summary>
        /// Bytes stored in this raw chunk.
        /// </summary>
        public ImmutableArray<byte> Data { get; init; } = ImmutableArray<byte>.Empty;


        public override byte[] Bytes => Data.ToArray();

        public override int ByteLength => Data.Length;

        public override int WriteTo(Span<byte> buffer)
        {
            for(int i = 0; i < Data.Length; ++i)
                buffer[i] = Data[i];

            return Data.Length;
        }

        /// <summary>
        /// Initializes the chunk from the given buffer.
        /// </summary>
        /// <param name="buffer">Buffer containing the chunk data.</param>
        /// <returns>Deserialized chunk object.</returns>
        public static RawSectionChunk FromBytes(ReadOnlySpan<byte> buffer)
        {
            // Copy data
            var builder = ImmutableArray.CreateBuilder<byte>(buffer.Length);
            foreach(var b in buffer)
                builder.Add(b);

            return new RawSectionChunk
            {
                Data = builder.MoveToImmutable()
            };
        }
    }
}