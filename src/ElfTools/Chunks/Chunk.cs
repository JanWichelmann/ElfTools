using System;
using System.Collections.Immutable;

namespace ElfTools.Chunks
{
    /// <summary>
    /// Represents a generic chunk.
    /// </summary>
    public abstract record Chunk
    {
        /// <summary>
        /// Returns the length of the chunk's byte representation.
        /// </summary>
        public abstract int ByteLength { get; }

        /// <summary>
        /// Returns the chunk's byte representation.
        /// </summary>
        /// <remarks>This property allocates and returns a new array, so changes to the array are not applied to the associated chunk object.</remarks>
        public virtual byte[] Bytes
        {
            get
            {
                var buffer = new byte[ByteLength];
                WriteTo(buffer);
                return buffer;
            }
        }

        /// <summary>
        /// Copies the chunk's byte representation into the given buffer.
        /// </summary>
        /// <param name="buffer">Target byte buffer. Must have space for at least <see cref="ByteLength" /> bytes.</param>
        /// <returns>Number of bytes written.</returns>
        public abstract int WriteTo(Span<byte> buffer);

        /// <summary>
        /// Converts this chunk instance to a dummy chunk.
        /// </summary>
        /// <returns>Dummy chunk which holds the same data as this instance.</returns>
        public DummyChunk ToDummyChunk()
        {
            return new DummyChunk
            {
                Data = Bytes.ToImmutableArray()
            };
        }
    }
}