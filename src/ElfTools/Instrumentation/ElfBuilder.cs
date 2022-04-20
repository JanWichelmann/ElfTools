using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using ElfTools.Chunks;
using ElfTools.Enums;

namespace ElfTools.Instrumentation
{
    /// <summary>
    /// Offers primitives for modifying or building ELF files.
    /// </summary>
    public class ElfBuilder
    {
        private readonly Action<IList<Chunk>, string>? _imageRenderer;
        private int _imageIndex = 0;

        /// <summary>
        /// The chunks the ELF file is made of, in file order.
        /// </summary>
        public List<Chunk> Chunks { get; }

        /// <summary>
        /// Reference to the ELF header chunk.
        /// </summary>
        public HeaderChunk Header { get; set; }

        /// <summary>
        /// Reference to the section header chunk.
        /// </summary>
        public SectionHeaderTableChunk SectionHeaderTable { get; set; }

        /// <summary>
        /// Reference to the program header chunk.
        /// </summary>
        public ProgramHeaderTableChunk ProgramHeaderTable { get; set; }

        /// <summary>
        /// Reference to the dynamic table section chunk.
        /// </summary>
        public DynamicTableChunk? DynamicTable { get; set; }

        /// <summary>
        /// Creates a new ELF builder from the given ELF data.
        /// </summary>
        /// <param name="elf">Base ELF file.</param>
        /// <param name="imageRenderer">(For debugging) Callback for rendering the chunk map as an image.</param>
        public ElfBuilder(ElfFile elf, Action<IList<Chunk>, string>? imageRenderer)
        {
            _imageRenderer = imageRenderer;

            // Initialize variables
            Chunks = elf.Chunks.ToList();
            Header = elf.Header;
            SectionHeaderTable = elf.SectionHeaderTable;
            ProgramHeaderTable = elf.ProgramHeaderTable ?? throw new ArgumentException("The ELF file must have a program header table.", nameof(elf));
            DynamicTable = elf.DynamicTable;

            // Some assumptions
            if(Header.ProgramHeaderTableFileOffset != Header.HeaderSize)
                throw new InvalidOperationException($"Invalid position of program header: Expected offset 0x{Header.HeaderSize:x8} (header size), found offset 0x{Header.ProgramHeaderTableFileOffset:x8}");
            if(Chunks[1] is not ProgramHeaderTableChunk)
                throw new InvalidOperationException("Could not find program header chunk at index #1");
        }

        /// <summary>
        /// Converts the internal data back into a <see cref="ElfFile"/> object.
        /// </summary>
        /// <returns></returns>
        public ElfFile ToElfFile()
        {
            return new ElfFile
            {
                Chunks = Chunks.ToImmutableList(),
                Header = Header,
                SectionHeaderTable = SectionHeaderTable,
                ProgramHeaderTable = ProgramHeaderTable,
                DynamicTable = DynamicTable
            };
        }

        /// <summary>
        /// Allocates a new dummy chunk inside the file memory which can subsequently be used to define new sections.
        /// </summary>
        /// <param name="offset">File offset. This must point to a chunk boundary or to a <see cref="DummyChunk"/>, i.e., it is not possible to expand an existing chunk.</param>
        /// <param name="size">Size of the allocated data range.</param>
        /// <remarks>
        /// This method may move other sections and will try to adjust possibly broken offsets in various tables.
        /// 
        /// This method does NOT check for broken relative offsets when inserting data which is covered by a LOAD segment.
        /// It may also break segment alignments, as it fully relies on the alignment values specified in the section headers.
        /// However, it extends segments to cover all data which was previously present in the respective segment.
        /// 
        /// Due to these limitations, this method should be used very sparsely for file offsets that are covered by or may move existing segments.
        /// </remarks>
        public void AllocateFileMemory(int offset, int size)
        {
            _imageRenderer?.Invoke(Chunks, $"chunks{(_imageIndex++):D3}.png");

            /*
             * Steps:
             * 1. Insert new dummy chunk with the given size.
             * 2. Try to move displaced sections back and ensure that they are aligned - possibly by eating some dummy chunks.
             * 3. Fix segmentation table by moving/extending entries.
             * 4. Update section table with new offsets.
             * 5. Fix other tables which use absolute offsets.
             * 6. (not necessary, but cleaner) Merge consecutive dummy chunks.
             */

            // Some sanity checks
            if(offset < Header.HeaderSize + ProgramHeaderTable.ByteLength)
                throw new ArgumentOutOfRangeException(nameof(offset), $"Cannot allocate space before end of program header (minimum offset is {(Header.HeaderSize + ProgramHeaderTable.ByteLength):x8})");
            if(size <= 0)
                throw new ArgumentOutOfRangeException(nameof(size), "The allocation size must be a positive integer.");

            /* Insert new dummy chunk */

            // Find affected chunk and insert new one
            int pos = 0;
            int newChunkIndex = -1;
            for(int i = 0; i < Chunks.Count; ++i)
            {
                if(pos == offset)
                {
                    // Insert new chunk at current position
                    newChunkIndex = i;
                    Chunks.Insert(newChunkIndex, new DummyChunk { Data = new byte[size].ToImmutableArray() });

                    break;
                }

                if(pos + Chunks[i].ByteLength == offset)
                {
                    // Insert new chunk after current one
                    newChunkIndex = i + 1;
                    Chunks.Insert(newChunkIndex, new DummyChunk { Data = new byte[size].ToImmutableArray() });

                    break;
                }

                if(pos < offset && offset < pos + Chunks[i].ByteLength && Chunks[i] is DummyChunk oldChunk)
                {
                    // Mid-chunk -> split
                    newChunkIndex = i + 1;

                    // Left half
                    Chunks[i] = DummyChunk.FromBytes(oldChunk.Data.AsSpan()[..(offset - pos)]);

                    // Our new chunk
                    Chunks.Insert(newChunkIndex, new DummyChunk { Data = new byte[size].ToImmutableArray() });

                    // Right half
                    Chunks.Insert(newChunkIndex + 1, DummyChunk.FromBytes(oldChunk.Data.AsSpan()[(offset - pos)..]));

                    break;
                }

                pos += Chunks[i].ByteLength;
            }

            _imageRenderer?.Invoke(Chunks, $"chunks{(_imageIndex++):D3}.png");

            // If we did not find a valid position for inserting a new chunk, throw an error
            if(newChunkIndex == -1)
                throw new ArgumentOutOfRangeException(nameof(offset), "The offset must point to a chunk boundary, a dummy chunk or the file end.");

            // Collect metadata of sections which may be affected by the resize
            List<(int sectionIndex, SectionHeaderTableChunk.SectionHeaderTableEntry sectionInfo)> oldSections = SectionHeaderTable.SectionHeaders
                .Select((section, index) => (index, section))
                .Where(s => s.section.Type != SectionType.NoBits && (int)s.section.FileOffset >= offset)
                .OrderBy(s => s.section.FileOffset)
                .ToList();


            /* Move and realign sections */

            // All other chunks after the current one moved. Try to reduce fallout by eating other dummy chunks
            Dictionary<int, (int chunkIndex, int newOffset, int delta)> movedSections = new();
            pos = offset + size;
            int remainingShift = size; // The number of bytes by which the chunks after pos are still shifted. We try to bring that to zero as quickly as possible
            int? newSectionHeaderTableOffset = null; // Non-null if there is a new offset
            const int sectionHeaderTableAlignment = 16; // There does not appear to be a mandatory alignment, so we just use a constant that appears to work
            for(int i = newChunkIndex + 1; i < Chunks.Count; ++i)
            {
                // If the following chunks are not shifted anymore, we are done
                if(remainingShift == 0)
                    break;
                Debug.Assert(remainingShift > 0);

                // Dummy chunk or section?
                if(Chunks[i] is DummyChunk dummyChunk)
                {
                    // This is a dummy chunk, we may eat it!
                    // Find next non-dummy chunk and check its alignment - hopefully we can reduce the size of the current dummy chunk.
                    while(i + 1 < Chunks.Count)
                    {
                        if(Chunks[i + 1] is DummyChunk anotherDummyChunk)
                        {
                            // Another dummy chunk! Weird. Merge them to keep stuff simple
                            Chunks[i] = dummyChunk = new DummyChunk { Data = dummyChunk.Data.Concat(anotherDummyChunk.Data).ToImmutableArray() };
                            Chunks.RemoveAt(i + 1);

                            // We don't need to increment i here
                        }
                        else if(Chunks[i + 1] is SectionChunk sectionChunk)
                        {
                            // A section chunk! Check and fix the alignment, while trying to shrink the preceding dummy chunk

                            int currentOffset = pos + dummyChunk.ByteLength;

                            // Find section metadata
                            var sectionData = oldSections.First(s => (int)s.sectionInfo.FileOffset == currentOffset - remainingShift);

                            // Compute deviation from alignment position
                            // We can safely assume that alignment is a power of two
                            int alignment = (int)sectionData.sectionInfo.Alignment;
                            if(alignment == 0)
                                alignment = 1;
                            int alignmentError = currentOffset & (alignment - 1);

                            // Align the section
                            // Try simultaneously to shrink the dummy chunk as much as possible
                            // If that does not work, we have to expand it slightly instead
                            int newOffset = currentOffset;
                            int newDummyChunkSize = dummyChunk.ByteLength;
                            if(alignmentError < newDummyChunkSize)
                            {
                                // First remove the alignment error
                                newOffset -= alignmentError;
                                newDummyChunkSize -= alignmentError;
                                remainingShift -= alignmentError;

                                // Try to remove some more multiples of the alignment
                                // Maybe we even manage to get the remaining shift to zero
                                while(remainingShift > 0 && alignment < newDummyChunkSize)
                                {
                                    newOffset -= alignment;
                                    newDummyChunkSize -= alignment;
                                    remainingShift -= alignment;
                                }
                            }
                            else
                            {
                                // The preceding dummy chunk is too small for a section re-alignment, we have to increase its size instead
                                int delta = alignment - alignmentError;
                                newOffset += delta;
                                newDummyChunkSize += delta;
                                remainingShift += delta;
                            }

                            // Update dummy chunk
                            if(newDummyChunkSize < dummyChunk.ByteLength)
                                Chunks[i] = dummyChunk = new DummyChunk { Data = dummyChunk.Data.Take(newDummyChunkSize).ToImmutableArray() };
                            else if(newDummyChunkSize > dummyChunk.ByteLength)
                                Chunks[i] = dummyChunk = new DummyChunk { Data = dummyChunk.Data.Concat(Enumerable.Repeat<byte>(0, newDummyChunkSize - dummyChunk.ByteLength)).ToImmutableArray() };

                            // Store section info for a later section table update, if the section's file offset has changed and we could not fix it above
                            if(newOffset != (int)sectionData.sectionInfo.FileOffset)
                                movedSections.Add(sectionData.sectionIndex, (i + 1, newOffset, newOffset - (int)sectionData.sectionInfo.FileOffset));

                            // Update current position
                            pos = newOffset + sectionChunk.ByteLength;

                            // We handled this chunk, so do not look at it again
                            ++i;
                            break;
                        }
                        else if(Chunks[i + 1] is SectionHeaderTableChunk sectionHeaderTableChunk)
                        {
                            // This is mostly copy/paste from above

                            // We found the section header table. Perform the same steps as for sections

                            int currentOffset = pos + dummyChunk.ByteLength;

                            // Compute deviation from alignment position
                            int alignment = sectionHeaderTableAlignment;
                            int alignmentError = currentOffset & (alignment - 1);

                            // Align the table
                            // Try simultaneously to shrink the dummy chunk as much as possible
                            // If that does not work, we have to expand it slightly instead
                            int newOffset = currentOffset;
                            int newDummyChunkSize = dummyChunk.ByteLength;
                            if(alignmentError < newDummyChunkSize)
                            {
                                // First remove the alignment error
                                newOffset -= alignmentError;
                                newDummyChunkSize -= alignmentError;
                                remainingShift -= alignmentError;

                                // Try to remove some more multiples of the alignment
                                // Maybe we even manage to get the remaining shift to zero
                                while(remainingShift > 0 && alignment < newDummyChunkSize)
                                {
                                    newOffset -= alignment;
                                    newDummyChunkSize -= alignment;
                                    remainingShift -= alignment;
                                }
                            }
                            else
                            {
                                // The preceding dummy chunk is too small for a section re-alignment, we have to increase its size instead
                                int delta = alignment - alignmentError;
                                newOffset += delta;
                                newDummyChunkSize += delta;
                                remainingShift += delta;
                            }

                            // Update dummy chunk
                            if(newDummyChunkSize < dummyChunk.ByteLength)
                                Chunks[i] = dummyChunk = new DummyChunk { Data = dummyChunk.Data.Take(newDummyChunkSize).ToImmutableArray() };
                            else if(newDummyChunkSize > dummyChunk.ByteLength)
                                Chunks[i] = dummyChunk = new DummyChunk { Data = dummyChunk.Data.Concat(Enumerable.Repeat<byte>(0, newDummyChunkSize - dummyChunk.ByteLength)).ToImmutableArray() };

                            // Remember if the offset was changed
                            if(newOffset != (int)Header.SectionHeaderTableFileOffset)
                                newSectionHeaderTableOffset = newOffset;

                            // Update current position
                            pos = newOffset + sectionHeaderTableChunk.ByteLength;

                            // We handled this chunk, so do not look at it again
                            ++i;
                            break;
                        }
                    }

                    _imageRenderer?.Invoke(Chunks, $"chunks{(_imageIndex++):D3}.png");

                    // If the loop terminates without finding another non-dummy chunk, we are fine - we have reached the end of the file
                }
                else if(Chunks[i] is SectionChunk sectionChunk)
                {
                    // This is a section
                    // We could only go in here because there was no dummy chunk before this section, as a section chunk immediately
                    // following a dummy chunk is handled in the above condition. We may need to insert a dummy chunk

                    // Find section metadata
                    int currentOffset = pos;
                    var sectionData = oldSections.First(s => (int)s.sectionInfo.FileOffset == currentOffset - remainingShift);

                    // Compute deviation from alignment position
                    // We can safely assume that alignment is a power of two
                    int alignment = (int)sectionData.sectionInfo.Alignment;
                    if(alignment == 0)
                        alignment = 1;
                    int alignmentError = currentOffset & (alignment - 1);

                    // Align the section
                    // We cannot shrink anything, so we have to insert a new dummy chunk
                    int newOffset = currentOffset;
                    if(alignmentError > 0)
                    {
                        // Create chunk
                        int delta = alignment - alignmentError;
                        Chunks.Insert(i, new DummyChunk { Data = new byte[delta].ToImmutableArray() });

                        newOffset += delta;
                        pos += delta;
                        remainingShift += delta;

                        // Skip the new dummy chunk
                        ++i;
                    }

                    // Store section info for a later section table update, if the section's file offset has changed and we could not fix it above
                    if(newOffset != (int)sectionData.sectionInfo.FileOffset)
                        movedSections.Add(sectionData.sectionIndex, (i, newOffset, newOffset - (int)sectionData.sectionInfo.FileOffset));

                    // Update current position
                    pos += sectionChunk.ByteLength;

                    _imageRenderer?.Invoke(Chunks, $"chunks{(_imageIndex++):D3}.png");
                }
                else if(Chunks[i] is SectionHeaderTableChunk sectionHeaderTableChunk)
                {
                    // This is mostly copy/paste from above

                    // Section header table - same handling as for sections

                    int currentOffset = pos;

                    // Compute deviation from alignment position
                    int alignment = sectionHeaderTableAlignment;
                    int alignmentError = currentOffset & (alignment - 1);

                    // Align the section
                    // We cannot shrink anything, so we have to insert a new dummy chunk
                    int newOffset = currentOffset;
                    if(alignmentError > 0)
                    {
                        // Create chunk
                        int delta = alignment - alignmentError;
                        Chunks.Insert(i, new DummyChunk { Data = new byte[delta].ToImmutableArray() });

                        newOffset += delta;
                        pos += delta;
                        remainingShift += delta;

                        // Skip the new dummy chunk
                        ++i;
                    }

                    // Remember if the offset was changed
                    if(newOffset != (int)Header.SectionHeaderTableFileOffset)
                        newSectionHeaderTableOffset = newOffset;

                    // Update current position
                    pos += sectionHeaderTableChunk.ByteLength;

                    _imageRenderer?.Invoke(Chunks, $"chunks{(_imageIndex++):D3}.png");
                }
            }

            _imageRenderer?.Invoke(Chunks, $"chunks{(_imageIndex++):D3}.png");


            /* Fix segments */

            // Segment base address:
            //   Sections cannot be moved to a position before their original one, so the base address can't decrease
            //   But it can increase, if the first section starts at a higher address than before the insertion
            // Segment end:
            //   A segment end address cannot become smaller
            //   But it can increase, if the _last_ section in the segment has grown beyond the segment end address.
            // We always increase the segment addresses if a section has moved - dummy space between section end and segment end is preserved.
            // (a "section" here also includes special data structures like the ELF header)

            // Get a list of all original sections, ordered by type
            const int sectionIndexElfHeader = -4;
            const int sectionIndexProgramHeader = -3;
            const int sectionIndexSectionHeader = -2;
            const int sectionIndexInvalid = -1;
            List<(int sectionIndex, int baseAddress)> sortedSections = oldSections
                .Select(sectionData => (sectionData.sectionIndex, (int)sectionData.sectionInfo.FileOffset))
                .Append((sectionIndexElfHeader, 0))
                .Append((sectionIndexProgramHeader, (int)Header.ProgramHeaderTableFileOffset))
                .Append((sectionIndexSectionHeader, (int)Header.SectionHeaderTableFileOffset))
                .OrderBy(s => s.Item2)
                .ToList();

            var programHeaderBuilder = ProgramHeaderTable.ProgramHeaders.ToBuilder();
            for(int i = 0; i < programHeaderBuilder.Count; ++i)
            {
                var segmentData = programHeaderBuilder[i];
                int segmentStartAddress = (int)segmentData.FileOffset;
                int segmentEndAddress = segmentStartAddress + (int)segmentData.FileSize;

                // Identify first and last "section" in the original segment
                int firstSectionIndex = sectionIndexInvalid;
                int lastSectionIndex = sectionIndexInvalid;

                // Iterate sections
                for(int j = 0; j < sortedSections.Count; ++j)
                {
                    var sectionData = sortedSections[j];
                    int sectionStartAddress = sectionData.baseAddress;

                    // Check whether section starts somewhere in the segment
                    if(segmentStartAddress <= sectionStartAddress && sectionStartAddress < segmentEndAddress)
                    {
                        if(firstSectionIndex == sectionIndexInvalid)
                            firstSectionIndex = sectionData.sectionIndex;
                        lastSectionIndex = sectionData.sectionIndex;
                    }
                }

                // Did the first section move? -> Move segment start address
                if(firstSectionIndex != sectionIndexInvalid)
                {
                    if(firstSectionIndex == sectionIndexElfHeader)
                    {
                        // Nothing to do here - the ELF header cannot be moved
                    }
                    else if(firstSectionIndex == sectionIndexProgramHeader)
                    {
                        // We also don't support moving the program header
                    }
                    else if(firstSectionIndex == sectionIndexSectionHeader)
                    {
                        // Check whether the section header table was moved
                        if(newSectionHeaderTableOffset != null)
                        {
                            // It moved, so adjust the segment start address
                            int delta = newSectionHeaderTableOffset.Value - (int)Header.SectionHeaderTableFileOffset;
                            segmentData = segmentData with
                            {
                                FileOffset = (ulong)((int)segmentData.FileOffset + delta),
                                // TODO we don't want to change section/segment virtual addresses, right? Too unreliable
                                //VirtualMemoryAddress = (ulong)((int)segmentData.VirtualMemoryAddress + delta),
                                //PhysicalMemoryAddress = (ulong)((int)segmentData.PhysicalMemoryAddress + delta),
                            };
                        }
                    }
                    else
                    {
                        // We have a standard section. Check whether it has moved
                        if(movedSections.TryGetValue(firstSectionIndex, out var movedSection))
                        {
                            // It moved, so adjust the segment start address
                            segmentData = segmentData with
                            {
                                FileOffset = (ulong)((int)segmentData.FileOffset + movedSection.delta),
                                //VirtualMemoryAddress = (ulong)((int)segmentData.VirtualMemoryAddress + movedSection.delta),
                                //PhysicalMemoryAddress = (ulong)((int)segmentData.PhysicalMemoryAddress + movedSection.delta),
                            };
                        }
                    }
                }

                // Did the last section move? -> Move segment end address
                // Note that we don't handle "growing" of a section here - this method only allocates dummy memory and moves stuff around.
                // Growth of a section/segment must be done and handled by the user (exception: allocating memory in mid of a segment usually leads to moving the last section)
                if(lastSectionIndex != sectionIndexInvalid && lastSectionIndex != firstSectionIndex)
                {
                    if(lastSectionIndex == sectionIndexElfHeader)
                    {
                        // Nothing to do here - the ELF header cannot be moved
                    }
                    else if(lastSectionIndex == sectionIndexProgramHeader)
                    {
                        // We also don't support moving or growing the program header
                    }
                    else if(lastSectionIndex == sectionIndexSectionHeader)
                    {
                        // Check whether the section header table was moved
                        if(newSectionHeaderTableOffset != null)
                        {
                            // It moved, so adjust the segment end address
                            int delta = newSectionHeaderTableOffset.Value - (int)Header.SectionHeaderTableFileOffset;
                            segmentData = segmentData with
                            {
                                FileSize = (ulong)((int)segmentData.FileSize + delta),
                                MemorySize = (ulong)((int)segmentData.MemorySize + delta)
                            };
                        }
                    }
                    else
                    {
                        // We have a standard section. Check whether it has moved
                        if(movedSections.TryGetValue(lastSectionIndex, out var movedSection))
                        {
                            // It moved, so adjust the segment end address
                            segmentData = segmentData with
                            {
                                FileSize = (ulong)((int)segmentData.FileSize + movedSection.delta),
                                MemorySize = (ulong)((int)segmentData.MemorySize + movedSection.delta)
                            };
                        }
                    }
                }

                programHeaderBuilder[i] = segmentData;
            }

            // Store updated program header table
            Chunks[1] = ProgramHeaderTable = ProgramHeaderTable with { ProgramHeaders = programHeaderBuilder.ToImmutable() };


            /* Update ELF header, if necessary */

            if(newSectionHeaderTableOffset != null)
            {
                Chunks[0] = Header = Header with
                {
                    SectionHeaderTableFileOffset = (ulong)newSectionHeaderTableOffset.Value
                };
            }


            /* Update sections headers */

            var sectionHeaderBuilder = SectionHeaderTable.SectionHeaders.ToBuilder();
            foreach(var movedSection in movedSections)
            {
                if(movedSection.Key < 0 || sectionHeaderBuilder.Count <= movedSection.Key)
                    continue;

                var oldSectionHeader = sectionHeaderBuilder[movedSection.Key];
                sectionHeaderBuilder[movedSection.Key] = sectionHeaderBuilder[movedSection.Key] with
                {
                    FileOffset = (ulong)((int)oldSectionHeader.FileOffset + movedSection.Value.delta),
                    //VirtualAddress = (ulong)((int)oldSectionHeader.VirtualAddress + movedSection.Value.delta)
                };
            }

            // Store updated section header table
            Chunks[Chunks.FindIndex(c => c is SectionHeaderTableChunk)] = SectionHeaderTable = SectionHeaderTable with { SectionHeaders = sectionHeaderBuilder.ToImmutable() };


            /* Fix tables */

            // We first compute a mapping of
            //   old section offset => (section size, new section offset)
            // for easier mapping
            Dictionary<int, (int size, int newOffset)> sectionAddressMoveMapping = oldSections.ToDictionary(s => (int)s.sectionInfo.FileOffset, s =>
            {
                if(movedSections.TryGetValue(s.sectionIndex, out var movedSection))
                    return ((int)s.sectionInfo.Size, movedSection.newOffset);

                return ((int)s.sectionInfo.Size, (int)s.sectionInfo.FileOffset);
            });

            int GetMovedSectionBaseOffset(int oldBaseOffset)
            {
                // Find matching section
                foreach(var (oldSectionOffset, (sectionSize, newSectionOffset)) in sectionAddressMoveMapping)
                {
                    // If this section matches, adjust the input offset accordingly
                    if(oldSectionOffset <= oldBaseOffset && oldBaseOffset < (oldSectionOffset + sectionSize))
                        return oldBaseOffset + newSectionOffset - oldSectionOffset; // Add offset delta
                }

                // Nothing has changed
                return oldBaseOffset;
            }

            // Fix sections mentioned in the dynamic table
            // We only support sections right now
            if(DynamicTable != null)
            {
                var dynamicTableBuilder = DynamicTable.Entries.ToBuilder();
                for(var i = 0; i < dynamicTableBuilder.Count; i++)
                {
                    var entry = dynamicTableBuilder[i];

                    switch(entry.Type)
                    {
                        // Adjust section offsets
                        case DynamicEntryType.DT_GNU_HASH:
                        case DynamicEntryType.DT_STRTAB:
                        case DynamicEntryType.DT_SYMTAB:
                        case DynamicEntryType.DT_JMPREL:
                        case DynamicEntryType.DT_REL:
                        case DynamicEntryType.DT_RELA:
                        case DynamicEntryType.DT_VERNEED:
                        case DynamicEntryType.DT_VERSYM:
                        case DynamicEntryType.DT_VERDEF:
                        {
                            dynamicTableBuilder[i] = entry with { Value = (ulong)GetMovedSectionBaseOffset((int)entry.Value) };
                            break;
                        }

                        // TODO Adjust segment offsets
                    }
                }

                Chunks[Chunks.FindIndex(c => c is DynamicTableChunk)] = DynamicTable = DynamicTable with { Entries = dynamicTableBuilder.ToImmutable() };
            }


            /* Merge consecutive dummy chunks */

            CleanUpDummyChunks();

            _imageRenderer?.Invoke(Chunks, $"chunks{(_imageIndex++):D3}.png");
        }

        /// <summary>
        /// Inserts new strings into the given string table section and returns the offsets of the inserted strings.
        /// </summary>
        /// <param name="sectionIndex">Section index of the string table.</param>
        /// <param name="newStrings">String(s) to insert.</param>
        /// <returns>The offsets of the newly inserted strings in the string table, in order.</returns>
        /// <remarks>The string table can only grow if there is sufficient dummy chunk space behind it.</remarks>
        public int[] ExtendStringTable(int sectionIndex, params string[] newStrings)
        {
            _imageRenderer?.Invoke(Chunks, $"chunks{(_imageIndex++):D3}.png");

            // Ensure that there are no consecutive dummy chunks (this way we always only need to deal with a single chunk)
            CleanUpDummyChunks();

            var sectionHeaderTableBuilder = SectionHeaderTable.SectionHeaders.ToBuilder();
            var stringTableChunkIndex = GetChunkIndexForOffset(sectionHeaderTableBuilder[sectionIndex].FileOffset);
            int newStringsLength = newStrings.Sum(s => s.Length + 1);

            if(stringTableChunkIndex == null || Chunks[stringTableChunkIndex.Value.chunkIndex] is not StringTableChunk stringTableChunk)
                throw new Exception("Could not resolve section index to string table chunk.");

            // Check whether there is enough space
            int dummyChunkIndex = stringTableChunkIndex.Value.chunkIndex + 1;
            if(dummyChunkIndex >= Chunks.Count || Chunks[dummyChunkIndex] is not DummyChunk dummyChunk || dummyChunk.ByteLength < newStringsLength)
                throw new Exception("Could not find sufficient dummy chunk space behind the existing string table section.");

            // Shrink dummy chunk
            var reducedDummyChunk = dummyChunk with { Data = dummyChunk.Data.Take(dummyChunk.ByteLength - newStringsLength).ToImmutableArray() };

            // Extend string table
            var extendedStringArray = stringTableChunk.Data.ToBuilder();

            // Temporarily remove last \0, as there is no InsertRange() method
            bool removeLastZero = extendedStringArray[^1] == '\0' && extendedStringArray[^2] == '\0';
            if(removeLastZero)
                extendedStringArray.RemoveAt(extendedStringArray.Count - 1);

            List<int> newStringsOffsets = new();
            foreach(var str in newStrings)
            {
                newStringsOffsets.Add(extendedStringArray.Count);
                extendedStringArray.AddRange(str);
                extendedStringArray.Add('\0');
            }

            if(removeLastZero)
                extendedStringArray.Add('\0'); // Re-add removed \0

            var extendedStringTableChunk = stringTableChunk with { Data = extendedStringArray.ToImmutable() };

            // Save modified chunks
            Chunks[stringTableChunkIndex.Value.chunkIndex] = extendedStringTableChunk;
            Chunks[dummyChunkIndex] = reducedDummyChunk;

            // Update section header table
            var oldSectionHeader = sectionHeaderTableBuilder[sectionIndex];
            sectionHeaderTableBuilder[sectionIndex] = oldSectionHeader with { Size = (ulong)((int)oldSectionHeader.Size + newStringsLength) };
            var newSectionHeaderTableChunk = SectionHeaderTable with { SectionHeaders = sectionHeaderTableBuilder.ToImmutable() };
            SectionHeaderTable = newSectionHeaderTableChunk;
            Chunks[Chunks.FindIndex(c => c is SectionHeaderTableChunk)] = newSectionHeaderTableChunk;

            _imageRenderer?.Invoke(Chunks, $"chunks{(_imageIndex++):D3}.png");

            return newStringsOffsets.ToArray();
        }

        /// <summary>
        /// Inserts new symbols into the given symbol table section.
        /// </summary>
        /// <param name="sectionIndex">Section index of the symbol table.</param>
        /// <param name="targetSectionIndex">Section index of the code/data the symbols are pointing to.</param>
        /// <param name="newSymbols">Symbols to insert.</param>
        /// <remarks>The symbol table can only grow if there is sufficient dummy chunk space behind it.</remarks>
        public void ExtendSymbolTable(int sectionIndex, int targetSectionIndex, List<(ulong offset, uint stringTableIndex)> newSymbols)
        {
            _imageRenderer?.Invoke(Chunks, $"chunks{(_imageIndex++):D3}.png");

            // Ensure that there are no consecutive dummy chunks (this way we always only need to deal with a single chunk)
            CleanUpDummyChunks();

            var sectionHeaderTableBuilder = SectionHeaderTable.SectionHeaders.ToBuilder();
            var oldSectionHeader = sectionHeaderTableBuilder[sectionIndex];
            var symbolTableChunkIndex = GetChunkIndexForOffset(oldSectionHeader.FileOffset);
            int newSymbolEntriesLength = newSymbols.Count * (int)oldSectionHeader.EntrySize;

            if(symbolTableChunkIndex == null || Chunks[symbolTableChunkIndex.Value.chunkIndex] is not SymbolTableChunk symbolTableChunk)
                throw new Exception("Could not resolve section index to symbol table chunk.");

            // Check whether there is enough space
            int dummyChunkIndex = symbolTableChunkIndex.Value.chunkIndex + 1;
            if(dummyChunkIndex >= Chunks.Count || Chunks[dummyChunkIndex] is not DummyChunk dummyChunk || dummyChunk.ByteLength < newSymbolEntriesLength)
                throw new Exception("Could not find sufficient dummy chunk space behind the existing symbol table section.");

            // Shrink dummy chunk
            var reducedDummyChunk = dummyChunk with { Data = dummyChunk.Data.Take(dummyChunk.ByteLength - newSymbolEntriesLength).ToImmutableArray() };

            // Extend symbol table
            // We have to insert local symbols before the global ones; pick an index before the last local one
            var extendedSymbolList = symbolTableChunk.Entries.ToBuilder();
            int insertionIndex = extendedSymbolList.FindLastIndex(s => (s.Info & SymbolInfo.MaskBind) == SymbolInfo.BindLocal);
            foreach(var symbol in newSymbols)
            {
                extendedSymbolList.Insert(insertionIndex++, new SymbolTableChunk.SymbolTableEntry
                {
                    Name = symbol.stringTableIndex,
                    Value = symbol.offset,
                    Size = 0,
                    Info = SymbolInfo.TypeFunc | SymbolInfo.BindLocal,
                    Visibility = SymbolVisibility.Default,
                    Section = (ushort)targetSectionIndex
                });
            }

            var extendedSymbolTableChunk = symbolTableChunk with { Entries = extendedSymbolList.ToImmutable() };

            // Save modified chunks
            Chunks[symbolTableChunkIndex.Value.chunkIndex] = extendedSymbolTableChunk;
            Chunks[dummyChunkIndex] = reducedDummyChunk;

            // Update section header table
            sectionHeaderTableBuilder[sectionIndex] = oldSectionHeader with
            {
                Size = (ulong)((int)oldSectionHeader.Size + newSymbolEntriesLength),
                Info = oldSectionHeader.Info + (uint)newSymbols.Count
            };
            var newSectionHeaderTableChunk = SectionHeaderTable with { SectionHeaders = sectionHeaderTableBuilder.ToImmutable() };
            SectionHeaderTable = newSectionHeaderTableChunk;
            Chunks[Chunks.FindIndex(c => c is SectionHeaderTableChunk)] = newSectionHeaderTableChunk;

            _imageRenderer?.Invoke(Chunks, $"chunks{(_imageIndex++):D3}.png");
        }

        /// <summary>
        /// Extends the given section by the given bytes.
        /// </summary>
        /// <param name="sectionIndex">Section index.</param>
        /// <param name="bytes">Bytes to add at the end.</param>
        /// <remarks>The section can only grow if there is sufficient dummy chunk space behind it.</remarks>
        public void ExtendRawSection(int sectionIndex, byte[] bytes)
        {
            _imageRenderer?.Invoke(Chunks, $"chunks{(_imageIndex++):D3}.png");

            // Ensure that there are no consecutive dummy chunks (this way we always only need to deal with a single chunk)
            CleanUpDummyChunks();

            var sectionHeaderTableBuilder = SectionHeaderTable.SectionHeaders.ToBuilder();
            var oldSectionHeader = sectionHeaderTableBuilder[sectionIndex];
            var sectionChunkIndex = GetChunkIndexForOffset(oldSectionHeader.FileOffset);

            if(sectionChunkIndex == null || Chunks[sectionChunkIndex.Value.chunkIndex] is not RawSectionChunk sectionChunk)
                throw new Exception("Could not resolve section index to section chunk.");

            // Check whether there is enough space
            int dummyChunkIndex = sectionChunkIndex.Value.chunkIndex + 1;
            if(dummyChunkIndex >= Chunks.Count || Chunks[dummyChunkIndex] is not DummyChunk dummyChunk || dummyChunk.ByteLength < bytes.Length)
                throw new Exception("Could not find sufficient dummy chunk space behind the existing section.");

            // Shrink dummy chunk
            var reducedDummyChunk = dummyChunk with { Data = dummyChunk.Data.Take(dummyChunk.ByteLength - bytes.Length).ToImmutableArray() };

            // Extend section
            var extendedSectionChunk = sectionChunk with { Data = sectionChunk.Data.Concat(bytes).ToImmutableArray() };

            // Save modified chunks
            Chunks[sectionChunkIndex.Value.chunkIndex] = extendedSectionChunk;
            Chunks[dummyChunkIndex] = reducedDummyChunk;

            // Update section header table
            sectionHeaderTableBuilder[sectionIndex] = oldSectionHeader with
            {
                Size = (ulong)((int)oldSectionHeader.Size + bytes.Length)
            };
            var newSectionHeaderTableChunk = SectionHeaderTable with { SectionHeaders = sectionHeaderTableBuilder.ToImmutable() };
            SectionHeaderTable = newSectionHeaderTableChunk;
            Chunks[Chunks.FindIndex(c => c is SectionHeaderTableChunk)] = newSectionHeaderTableChunk;

            _imageRenderer?.Invoke(Chunks, $"chunks{(_imageIndex++):D3}.png");
        }

        /// <summary>
        /// Creates a new section based on the given new section header table entry.
        /// </summary>
        /// <param name="newSectionHeaderTableEntry">The entry to add to the section header table.</param>
        /// <returns>Index of the newly created section.</returns>
        /// <remarks>Both the space needed for the new section and the section header table must have been pre-allocated as dummy chunks.</remarks>
        public int CreateSection(SectionHeaderTableChunk.SectionHeaderTableEntry newSectionHeaderTableEntry)
        {
            _imageRenderer?.Invoke(Chunks, $"chunks{(_imageIndex++):D3}.png");

            // Ensure that there are no consecutive dummy chunks (this way we always only need to deal with a single chunk)
            CleanUpDummyChunks();

            var sectionHeaderTableBuilder = SectionHeaderTable.SectionHeaders.ToBuilder();
            int sectionHeaderTableChunkIndex = Chunks.FindIndex(c => c is SectionHeaderTableChunk);
            if(sectionHeaderTableChunkIndex == -1)
                throw new Exception("Could not find section header table chunk.");

            // Check whether there is enough space in the section header table
            int sectionHeaderTableDummyChunkIndex = sectionHeaderTableChunkIndex + 1;
            if(sectionHeaderTableDummyChunkIndex >= Chunks.Count || Chunks[sectionHeaderTableDummyChunkIndex] is not DummyChunk sectionHeaderTableDummyChunk || sectionHeaderTableDummyChunk.ByteLength < SectionHeaderTable.EntrySize)
                throw new Exception("Could not find sufficient dummy chunk space behind the existing section header table.");

            // Compute space needed for the new section
            var chunkAtSectionOffset = GetChunkIndexForOffset(newSectionHeaderTableEntry.FileOffset);
            if(chunkAtSectionOffset == null || Chunks[chunkAtSectionOffset.Value.chunkIndex] is not DummyChunk sectionDummyChunk)
                throw new Exception("Could not find a dummy chunk at the desired offset.");
            int newSectionOffset = (int)newSectionHeaderTableEntry.FileOffset;
            int newSectionSize = (int)newSectionHeaderTableEntry.Size;
            int gapBytes = newSectionOffset - (int)chunkAtSectionOffset.Value.chunkBaseOffset;
            if(sectionDummyChunk.ByteLength < gapBytes + newSectionSize)
                throw new Exception("The dummy chunk at the given offset is too small to fit the section with the requested alignment.");
            if(chunkAtSectionOffset.Value.chunkIndex == sectionHeaderTableDummyChunkIndex && sectionDummyChunk.ByteLength < SectionHeaderTable.EntrySize + newSectionSize)
                throw new Exception("The dummy chunk at the given offset is too small to fit a section header table entry and the section with the requested alignment.");

            // Create section chunk
            // sectionDummyChunk -> gap | newSection | gap
            int pos = chunkAtSectionOffset.Value.chunkIndex;
            Chunks.RemoveAt(pos);
            if(gapBytes > 0)
                Chunks.Insert(pos++, new DummyChunk { Data = sectionDummyChunk.Data.Take(gapBytes).ToImmutableArray() });
            Chunks.Insert(pos++, new RawSectionChunk { Data = sectionDummyChunk.Data.Skip(gapBytes).Take(newSectionSize).ToImmutableArray() });
            if(sectionDummyChunk.ByteLength > gapBytes + newSectionSize)
                Chunks.Insert(pos++, new DummyChunk { Data = sectionDummyChunk.Data.Skip(gapBytes + newSectionSize).ToImmutableArray() });

            // Fix chunk index of section header table
            if(sectionHeaderTableChunkIndex > chunkAtSectionOffset.Value.chunkIndex)
            {
                sectionHeaderTableChunkIndex += pos - 1 - chunkAtSectionOffset.Value.chunkIndex;
                sectionHeaderTableDummyChunkIndex += pos - 1 - chunkAtSectionOffset.Value.chunkIndex;
            }

            // Find appropriate section index for new table entry
            int newSectionIndex = 0;
            while(newSectionIndex < sectionHeaderTableBuilder.Count)
            {
                var currentSectionTableHeader = sectionHeaderTableBuilder[newSectionIndex];
                if(newSectionHeaderTableEntry.FileOffset < currentSectionTableHeader.FileOffset)
                    break;

                ++newSectionIndex;
            }

            // Insert new entry
            sectionHeaderTableBuilder.Insert(newSectionIndex, newSectionHeaderTableEntry);

            // Fix index of section header string table in ELF header
            int sectionHeaderStringTableIndex = Header.SectionHeaderStringTableIndex;
            if(newSectionIndex <= Header.SectionHeaderStringTableIndex)
                ++sectionHeaderStringTableIndex;

            // Update ELF header
            Chunks[0] = Header = Header with
            {
                SectionHeaderStringTableIndex = (ushort)sectionHeaderStringTableIndex,
                SectionHeaderTableEntryCount = (ushort)sectionHeaderTableBuilder.Count
            };

            // Save section header table chunks
            Chunks[sectionHeaderTableChunkIndex] = SectionHeaderTable = new SectionHeaderTableChunk
            {
                EntrySize = SectionHeaderTable.EntrySize,
                SectionHeaders = sectionHeaderTableBuilder.ToImmutable()
            };
            sectionHeaderTableDummyChunk = (DummyChunk)Chunks[sectionHeaderTableDummyChunkIndex]; // The dummy chunk may be the same as the one used for allocating the new section, so ensure we have an up-to-date version
            Chunks[sectionHeaderTableDummyChunkIndex] = sectionHeaderTableDummyChunk with { Data = sectionHeaderTableDummyChunk.Data.Take(sectionHeaderTableDummyChunk.ByteLength - SectionHeaderTable.EntrySize).ToImmutableArray() };

            _imageRenderer?.Invoke(Chunks, $"chunks{(_imageIndex++):D3}.png");

            return newSectionIndex;
        }

        /// <summary>
        /// Inserts a new entry into the program header table.
        /// </summary>
        /// <param name="newProgramHeaderTableEntry">The new program header table entry.</param>
        /// <remarks>The table can only grow if there is sufficient dummy chunk space behind it.</remarks>
        public void ExtendProgramHeaderTable(ProgramHeaderTableChunk.ProgramHeaderTableEntry newProgramHeaderTableEntry)
        {
            _imageRenderer?.Invoke(Chunks, $"chunks{(_imageIndex++):D3}.png");

            // Ensure that there are no consecutive dummy chunks (this way we always only need to deal with a single chunk)
            CleanUpDummyChunks();

            const int programHeaderTableChunkIndex = 1;

            // Check whether there is enough space
            int dummyChunkIndex = programHeaderTableChunkIndex + 1;
            if(dummyChunkIndex >= Chunks.Count || Chunks[dummyChunkIndex] is not DummyChunk dummyChunk || dummyChunk.ByteLength < ProgramHeaderTable.EntrySize)
                throw new Exception("Could not find sufficient dummy chunk space behind the existing table chunk.");

            // Shrink dummy chunk
            var reducedDummyChunk = dummyChunk with { Data = dummyChunk.Data.Take(dummyChunk.ByteLength - ProgramHeaderTable.EntrySize).ToImmutableArray() };

            // Extend table
            var programHeaderTableBuilder = ProgramHeaderTable.ProgramHeaders.ToBuilder();

            // Find appropriate index for new table entry
            // We first look for entries with the same type, then insert such that the virtual memory addresses are in increasing order
            int insertionIndex = 0;
            for(; insertionIndex < programHeaderTableBuilder.Count; ++insertionIndex)
                if(programHeaderTableBuilder[insertionIndex].Type == newProgramHeaderTableEntry.Type)
                    break;
            while(insertionIndex < programHeaderTableBuilder.Count)
            {
                var currentProgramTableHeader = programHeaderTableBuilder[insertionIndex];
                if(newProgramHeaderTableEntry.VirtualMemoryAddress < currentProgramTableHeader.VirtualMemoryAddress || currentProgramTableHeader.Type != newProgramHeaderTableEntry.Type)
                    break;

                ++insertionIndex;
            }

            // Insert new entry
            programHeaderTableBuilder.Insert(insertionIndex, newProgramHeaderTableEntry);

            // Update ELF header
            Chunks[0] = Header = Header with
            {
                ProgramHeaderTableEntryCount = (ushort)programHeaderTableBuilder.Count,
            };

            // Save modified chunks
            Chunks[programHeaderTableChunkIndex] = ProgramHeaderTable = new ProgramHeaderTableChunk
            {
                EntrySize = ProgramHeaderTable.EntrySize,
                ProgramHeaders = programHeaderTableBuilder.ToImmutable()
            };
            Chunks[dummyChunkIndex] = reducedDummyChunk;

            _imageRenderer?.Invoke(Chunks, $"chunks{(_imageIndex++):D3}.png");
        }

        public void PatchValueInRelocationTable(ulong offset, long oldValue, long newValue)
        {
            // Find relocation table
            for(int i = 0; i < Chunks.Count; ++i)
            {
                if(Chunks[i] is not RelocationAddendTableChunk relocationTableChunk)
                    continue;

                var tableBuilder = relocationTableChunk.Entries.ToBuilder();
                for(int j = 0; j < tableBuilder.Count; ++j)
                {
                    var tableEntry = tableBuilder[j];
                    if(tableEntry.Offset == offset && tableEntry.Addend==oldValue)
                    {
                        tableBuilder[j] = tableEntry with { Addend = newValue };
                    }
                }

                Chunks[i] = relocationTableChunk with { Entries = tableBuilder.ToImmutable() };
            }
            
        }
        
        /// <summary>
        /// Reads bytes from the given file offset.
        /// The accessed bytes must reside in a raw section chunk.
        /// </summary>
        /// <param name="offset">Offset where the bytes should be read.</param>
        /// <param name="bytes">Buffer for the read bytes.</param>
        public void GetRawBytesAtOffset(int offset, Span<byte> bytes)
        {
            // Resolve chunk
            (int chunkIndex, ulong chunkBaseOffset) = GetChunkIndexForOffset((ulong)offset)
                                                      ?? throw new Exception("Could not locate chunk belonging to the given offset.");
            if(Chunks[chunkIndex] is not RawSectionChunk rawSectionChunk)
                throw new InvalidOperationException("This method can only read raw section chunks.");

            // Patch chunk data
            int relativeChunkOffset = offset - (int)chunkBaseOffset;
            for(int i = 0; i < bytes.Length; ++i)
                bytes[i] = rawSectionChunk.Data[relativeChunkOffset + i];
        }

        /// <summary>
        /// Replaces a number of bytes at the given file offset.
        /// The replaced bytes must reside in a raw section chunk.
        /// </summary>
        /// <param name="offset">Offset where the bytes should be replaced.</param>
        /// <param name="newBytes">New bytes.</param>
        public void PatchRawBytesAtOffset(int offset, ReadOnlySpan<byte> newBytes)
        {
            // Resolve chunk
            (int chunkIndex, ulong chunkBaseOffset) = GetChunkIndexForOffset((ulong)offset)
                                                      ?? throw new Exception("Could not locate chunk belonging to the given offset.");
            if(Chunks[chunkIndex] is not RawSectionChunk rawSectionChunk)
                throw new InvalidOperationException("This method can only patch raw section chunks.");

            // Patch chunk data
            int relativeChunkOffset = offset - (int)chunkBaseOffset;
            var chunkDataBuilder = rawSectionChunk.Data.ToBuilder();
            for(int i = 0; i < newBytes.Length; ++i)
                chunkDataBuilder[relativeChunkOffset + i] = newBytes[i];

            // Store updated chunk
            Chunks[chunkIndex] = new RawSectionChunk { Data = chunkDataBuilder.ToImmutable() };
        }

        /// <summary>
        /// Replaces a number of bytes at the given virtual address, as determined by the program header table.
        /// The replaced bytes must reside in a raw section chunk.
        /// </summary>
        /// <param name="address">Virtual address where the bytes should be replaced.</param>
        /// <param name="newBytes">New bytes.</param>
        public void PatchRawBytesAtAddress(int address, ReadOnlySpan<byte> newBytes)
        {
            // Resolve segment
            int endAddress = address + newBytes.Length;
            var programHeaderTableEntry = ProgramHeaderTable.ProgramHeaders.First(ph => (int)ph.VirtualMemoryAddress <= address && endAddress <= (int)(ph.VirtualMemoryAddress + ph.FileSize));

            // Resolve chunk
            int relativeSegmentOffset = address - (int)programHeaderTableEntry.VirtualMemoryAddress;
            (int chunkIndex, ulong chunkBaseOffset) = GetChunkIndexForOffset(programHeaderTableEntry.FileOffset + (ulong)relativeSegmentOffset)
                                                      ?? throw new Exception("Could not locate chunk belonging to the given offset.");
            if(Chunks[chunkIndex] is not RawSectionChunk rawSectionChunk)
                throw new InvalidOperationException("This method can only patch raw section chunks.");

            // Patch chunk data
            int relativeChunkOffset = (int)programHeaderTableEntry.FileOffset + relativeSegmentOffset - (int)chunkBaseOffset;
            var chunkDataBuilder = rawSectionChunk.Data.ToBuilder();
            for(int i = 0; i < newBytes.Length; ++i)
                chunkDataBuilder[relativeChunkOffset + i] = newBytes[i];

            // Store updated chunk
            Chunks[chunkIndex] = new RawSectionChunk { Data = chunkDataBuilder.ToImmutable() };
        }

        /// <summary>
        /// Merges consecutive dummy chunks and removes empty ones.
        /// </summary>
        public void CleanUpDummyChunks()
        {
            for(int i = 0; i < Chunks.Count;)
            {
                if(Chunks[i] is not DummyChunk dummyChunk)
                {
                    ++i;
                    continue;
                }

                // Look for more dummy chunks
                while(i + 1 < Chunks.Count && Chunks[i + 1] is DummyChunk anotherDummyChunk)
                {
                    // Merge chunks
                    Chunks[i] = dummyChunk = new DummyChunk { Data = dummyChunk.Data.Concat(anotherDummyChunk.Data).ToImmutableArray() };
                    Chunks.RemoveAt(i + 1);
                }

                // If the dummy chunk is empty, remove it altogether
                if(dummyChunk.ByteLength == 0)
                    Chunks.RemoveAt(i);
                else
                    ++i;
            }
        }

        /// <summary>
        /// Maps the given file offset to the corresponding chunk index.
        /// </summary>
        /// <param name="offset">File offset (may point to any position in a chunk).</param>
        /// <returns>A tuple consisting of the chunk index and the chunk's base file offset. If the corresponding chunk cannot be located, this method returns null.</returns>
        public (int chunkIndex, ulong chunkBaseOffset)? GetChunkIndexForOffset(ulong offset)
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
    }
}