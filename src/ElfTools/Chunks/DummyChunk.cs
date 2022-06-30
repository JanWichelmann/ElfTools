using System;
using System.Collections.Immutable;
using System.Linq;

namespace ElfTools.Chunks
{
    /// <summary>
    /// A dummy chunk for storing data which does not belong to headers or section.
    /// Used for alignment bytes between sections.
    /// </summary>
    public class DummyChunk : Chunk
    {
        /// <summary>
        /// Bytes stored in this dummy chunk.
        /// </summary>
        public byte[] Data { get; set; }


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
        public static DummyChunk FromBytes(ReadOnlySpan<byte> buffer)
        {
            // Copy data
            byte[] data = new byte[buffer.Length];
            buffer.CopyTo(data);

            return new DummyChunk
            {
                Data = data
            };
        }
    }
}