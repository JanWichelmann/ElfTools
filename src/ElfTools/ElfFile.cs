using System.Collections.Immutable;
using System.Linq;
using ElfTools.Chunks;

namespace ElfTools
{
    /// <summary>
    /// Represents an ELF file.
    /// </summary>
    public record ElfFile
    {
        /// <summary>
        /// The chunks this ELF file is made of, in file order.
        /// </summary>
        public ImmutableList<Chunk> Chunks { get; init; } = ImmutableList<Chunk>.Empty;

        /// <summary>
        /// Reference to the ELF header chunk.
        /// </summary>
        public HeaderChunk Header { get; init; } = new();

        /// <summary>
        /// Reference to the section header chunk.
        /// </summary>
        public SectionHeaderTableChunk SectionHeaderTable { get; init; } = new();

        /// <summary>
        /// Reference to the program header chunk.
        /// </summary>
        public ProgramHeaderTableChunk? ProgramHeaderTable { get; init; } = new();

        /// <summary>
        /// Reference to the dynamic table section chunk.
        /// </summary>
        public DynamicTableChunk? DynamicTable { get; init; }

        /// <summary>
        /// Maps the given file offset to the corresponding chunk object.
        /// </summary>
        /// <param name="offset">File offset (may point to any position in a chunk).</param>
        /// <returns>A tuple consisting of the chunk index and the chunk's base file offset. If the corresponding chunk cannot be located, this method returns null.</returns>
        public (int chunkIndex, ulong chunkBaseOffset)? GetChunkAtFileOffset(ulong offset)
        {
            // Find chunk
            // The chunk list is ordered by offset, so we can just traverse it
            int address = 0;
            for(var index = 0; index < Chunks.Count; index++)
            {
                var chunk = Chunks[index];
                int chunkEnd = address + chunk.ByteLength;
                if(chunkEnd > (int)offset)
                    return (index, (ulong)address);

                address = chunkEnd;
            }

            // Not found
            return null;
        }

        /// <summary>
        /// Returns the total byte length of all chunks, which matches the total length of the ELF file.
        /// </summary>
        /// <returns>Total byte length of all contained chunks.</returns>
        public int GetByteLength()
        {
            return Chunks.Sum(c => c.ByteLength);
        }

        /// <summary>
        /// Resolves the given virtual address to a file offset.
        /// </summary>
        /// <param name="address">Virtual address.</param>
        /// <returns>File offset, or -1 if the address wasn't found.</returns>
        public int GetFileOffsetForAddress(ulong address)
        {
            // Look at program header table entries
            foreach(var programHeader in ProgramHeaderTable.ProgramHeaders)
            {
                if(programHeader.VirtualMemoryAddress <= address && address < programHeader.VirtualMemoryAddress + programHeader.FileSize)
                    return (int)(programHeader.FileOffset + address - programHeader.VirtualMemoryAddress);
            }

            return -1;
        }
    }
}