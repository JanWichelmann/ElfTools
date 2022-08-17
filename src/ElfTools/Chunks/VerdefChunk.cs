using System;
using System.Collections.Generic;
using System.Linq;
using ElfTools.Enums;
using ElfTools.Utilities;

namespace ElfTools.Chunks
{
    public class VerdefChunk : SectionChunk
    {
        /// <summary>
        /// Bytes stored in this raw chunk.
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
        public static VerdefChunk FromBytes(ReadOnlySpan<byte> buffer)
        {
            // Copy data
            byte[] data = new byte[buffer.Length];
            buffer.CopyTo(data);

            return new VerdefChunk
            {
                Data = data
            };
        }
    }
}