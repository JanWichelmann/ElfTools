using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ElfTools.Chunks;
using ElfTools.Enums;

namespace ElfTools.Instrumentation
{
    /// <summary>
    /// Offers extension functions for modifying ELF files.
    /// Assumption of allocation functionality: The program header directly follows the file header.
    /// </summary>
    public static class ElfBuilder
    {
        private static int _imageIndex = 0;

        /// <summary>
        /// Allocates a new dummy chunk inside the file memory which can subsequently be used to define new sections.
        /// </summary>
        /// <param name="elf">ELF file.</param>
        /// <param name="offset">File offset. This must point to a chunk boundary or to a <see cref="DummyChunk"/>, i.e., it is not possible to expand an existing chunk.</param>
        /// <param name="size">Size of the allocated data range.</param>
        /// <param name="imageRenderer">(For debugging) Callback for rendering the chunk map as an image.</param>
        /// <remarks>
        /// This method may move other sections and will try to adjust possibly broken offsets in various tables.
        /// 
        /// This method does NOT check for broken relative offsets when inserting data which is covered by a LOAD segment.
        /// It may also break segment alignments, as it fully relies on the alignment values specified in the section headers.
        /// However, it extends segments to cover all data which was previously present in the respective segment.
        /// 
        /// Due to these limitations, this method should be used very sparsely for file offsets that are covered by or may move existing segments.
        /// </remarks>
        public static void AllocateFileMemory(this ElfFile elf, int offset, int size, Action<IList<Chunk>, string>? imageRenderer = null)
        {
            imageRenderer?.Invoke(elf.Chunks, $"chunks{(_imageIndex++):D3}.png");

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
            if(offset < elf.Header.HeaderSize + elf.ProgramHeaderTable!.ByteLength)
                throw new ArgumentOutOfRangeException(nameof(offset), $"Cannot allocate space before end of program header (minimum offset is {(elf.Header.HeaderSize + elf.ProgramHeaderTable.ByteLength):x8})");
            if(size <= 0)
                throw new ArgumentOutOfRangeException(nameof(size), "The allocation size must be a positive integer.");

            /* Insert new dummy chunk */

            // Find affected chunk and insert new one
            int pos = 0;
            int newChunkIndex = -1;
            for(int i = 0; i < elf.Chunks.Count; ++i)
            {
                if(pos == offset)
                {
                    // Insert new chunk at current position
                    newChunkIndex = i;
                    elf.Chunks.Insert(newChunkIndex, new DummyChunk { Data = new byte[size] });

                    break;
                }

                if(pos + elf.Chunks[i].ByteLength == offset)
                {
                    // Insert new chunk after current one
                    newChunkIndex = i + 1;
                    elf.Chunks.Insert(newChunkIndex, new DummyChunk { Data = new byte[size] });

                    break;
                }

                if(pos < offset && offset < pos + elf.Chunks[i].ByteLength && elf.Chunks[i] is DummyChunk oldChunk)
                {
                    // Mid-chunk -> split
                    newChunkIndex = i + 1;

                    // Left half
                    elf.Chunks[i] = DummyChunk.FromBytes(oldChunk.Data.AsSpan()[..(offset - pos)]);

                    // Our new chunk
                    elf.Chunks.Insert(newChunkIndex, new DummyChunk { Data = new byte[size] });

                    // Right half
                    elf.Chunks.Insert(newChunkIndex + 1, DummyChunk.FromBytes(oldChunk.Data.AsSpan()[(offset - pos)..]));

                    break;
                }

                pos += elf.Chunks[i].ByteLength;
            }

            imageRenderer?.Invoke(elf.Chunks, $"chunks{(_imageIndex++):D3}.png");

            // If we did not find a valid position for inserting a new chunk, throw an error
            if(newChunkIndex == -1)
                throw new ArgumentOutOfRangeException(nameof(offset), "The offset must point to a chunk boundary, a dummy chunk or the file end.");

            // Collect metadata of sections which may be affected by the resize
            List<(int sectionIndex, SectionHeaderTableChunk.SectionHeaderTableEntry sectionInfo)> oldSections = elf.SectionHeaderTable.SectionHeaders
                .Select((section, index) => (index, section: (SectionHeaderTableChunk.SectionHeaderTableEntry)section.Clone()))
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
            for(int i = newChunkIndex + 1; i < elf.Chunks.Count; ++i)
            {
                // If the following chunks are not shifted anymore, we are done
                if(remainingShift == 0)
                    break;
                Debug.Assert(remainingShift > 0);

                // Dummy chunk or section?
                if(elf.Chunks[i] is DummyChunk dummyChunk)
                {
                    // This is a dummy chunk, we may eat it!
                    // Find next non-dummy chunk and check its alignment - hopefully we can reduce the size of the current dummy chunk.
                    while(i + 1 < elf.Chunks.Count)
                    {
                        if(elf.Chunks[i + 1] is DummyChunk anotherDummyChunk)
                        {
                            // Another dummy chunk! Weird. Merge them to keep stuff simple
                            dummyChunk.Data = dummyChunk.Data.Concat(anotherDummyChunk.Data).ToArray();
                            elf.Chunks.RemoveAt(i + 1);

                            // We don't need to increment i here
                        }
                        else if(elf.Chunks[i + 1] is SectionChunk sectionChunk)
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
                                dummyChunk.Data = dummyChunk.Data.Take(newDummyChunkSize).ToArray();
                            else if(newDummyChunkSize > dummyChunk.ByteLength)
                                dummyChunk.Data = dummyChunk.Data.Concat(Enumerable.Repeat<byte>(0, newDummyChunkSize - dummyChunk.ByteLength)).ToArray();

                            // Store section info for a later section table update, if the section's file offset has changed and we could not fix it above
                            if(newOffset != (int)sectionData.sectionInfo.FileOffset)
                                movedSections.Add(sectionData.sectionIndex, (i + 1, newOffset, newOffset - (int)sectionData.sectionInfo.FileOffset));

                            // Update current position
                            pos = newOffset + sectionChunk.ByteLength;

                            // We handled this chunk, so do not look at it again
                            ++i;
                            break;
                        }
                        else if(elf.Chunks[i + 1] is SectionHeaderTableChunk sectionHeaderTableChunk)
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
                                dummyChunk.Data = dummyChunk.Data.Take(newDummyChunkSize).ToArray();
                            else if(newDummyChunkSize > dummyChunk.ByteLength)
                                dummyChunk.Data = dummyChunk.Data.Concat(Enumerable.Repeat<byte>(0, newDummyChunkSize - dummyChunk.ByteLength)).ToArray();

                            // Remember if the offset was changed
                            if(newOffset != (int)elf.Header.SectionHeaderTableFileOffset)
                                newSectionHeaderTableOffset = newOffset;

                            // Update current position
                            pos = newOffset + sectionHeaderTableChunk.ByteLength;

                            // We handled this chunk, so do not look at it again
                            ++i;
                            break;
                        }
                    }

                    imageRenderer?.Invoke(elf.Chunks, $"chunks{(_imageIndex++):D3}.png");

                    // If the loop terminates without finding another non-dummy chunk, we are fine - we have reached the end of the file
                }
                else if(elf.Chunks[i] is SectionChunk sectionChunk)
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
                        elf.Chunks.Insert(i, new DummyChunk { Data = new byte[delta] });

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

                    imageRenderer?.Invoke(elf.Chunks, $"chunks{(_imageIndex++):D3}.png");
                }
                else if(elf.Chunks[i] is SectionHeaderTableChunk sectionHeaderTableChunk)
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
                        elf.Chunks.Insert(i, new DummyChunk { Data = new byte[delta] });

                        newOffset += delta;
                        pos += delta;
                        remainingShift += delta;

                        // Skip the new dummy chunk
                        ++i;
                    }

                    // Remember if the offset was changed
                    if(newOffset != (int)elf.Header.SectionHeaderTableFileOffset)
                        newSectionHeaderTableOffset = newOffset;

                    // Update current position
                    pos += sectionHeaderTableChunk.ByteLength;

                    imageRenderer?.Invoke(elf.Chunks, $"chunks{(_imageIndex++):D3}.png");
                }
            }

            imageRenderer?.Invoke(elf.Chunks, $"chunks{(_imageIndex++):D3}.png");


            /* Fix segments */

            /*
            In the following, we adjust the segment addresses for all sections _within a segment_. We don't touch sections which aren't
            covered by a segment. We also don't touch LOAD segments at the moment, as those may be referred to from the code. Their
            correctness must be ensured by the user.
            
            Segment base address:
              Sections cannot be moved to a position before their original one, so the base address can't decrease
              But it can increase, if the first section starts at a higher address than before the insertion
            Segment end:
              A segment end address cannot become smaller
              But it can increase, if the _last_ section in the segment has grown beyond the segment end address.
           
            We increase the segment addresses if a section has moved - dummy space between section end and segment end is preserved.
            (a "section" here also includes special data structures like the ELF header)
            */

            // Get a list of all original sections, ordered by type
            const int sectionIndexElfHeader = -4;
            const int sectionIndexProgramHeader = -3;
            const int sectionIndexSectionHeader = -2;
            const int sectionIndexInvalid = -1;
            List<(int sectionIndex, int baseAddress)> sortedSections = oldSections
                .Select(sectionData => (sectionData.sectionIndex, (int)sectionData.sectionInfo.FileOffset))
                .Append((sectionIndexElfHeader, 0))
                .Append((sectionIndexProgramHeader, (int)elf.Header.ProgramHeaderTableFileOffset))
                .Append((sectionIndexSectionHeader, (int)elf.Header.SectionHeaderTableFileOffset))
                .OrderBy(s => s.Item2)
                .ToList();

            for(int i = 0; i < elf.ProgramHeaderTable.ProgramHeaders.Count; ++i)
            {
                var segmentData = elf.ProgramHeaderTable.ProgramHeaders[i];
                int segmentStartAddress = (int)segmentData.FileOffset;
                int segmentEndAddress = segmentStartAddress + (int)segmentData.FileSize;

                // Identify first and last "section" in the original segment
                int firstSectionIndex = sectionIndexInvalid;
                int lastSectionIndex = sectionIndexInvalid;

                // Iterate sections
                foreach(var sectionData in sortedSections)
                {
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
                    // Handle different section types
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
                            int delta = newSectionHeaderTableOffset.Value - (int)elf.Header.SectionHeaderTableFileOffset;
                            segmentData.FileOffset = (ulong)((int)segmentData.FileOffset + delta);

                            if(segmentData.Type != SegmentType.Load)
                            {
                                segmentData.VirtualMemoryAddress = (ulong)((int)segmentData.VirtualMemoryAddress + delta);
                                segmentData.PhysicalMemoryAddress = (ulong)((int)segmentData.PhysicalMemoryAddress + delta);
                            }
                        }
                    }
                    else
                    {
                        // We have a standard section. Check whether it has moved
                        if(movedSections.TryGetValue(firstSectionIndex, out var movedSection))
                        {
                            // It moved, so adjust the segment start address
                            segmentData.FileOffset = (ulong)((int)segmentData.FileOffset + movedSection.delta);

                            if(segmentData.Type != SegmentType.Load)
                            {
                                segmentData.VirtualMemoryAddress = (ulong)((int)segmentData.VirtualMemoryAddress + movedSection.delta);
                                segmentData.PhysicalMemoryAddress = (ulong)((int)segmentData.PhysicalMemoryAddress + movedSection.delta);
                            }
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
                            int delta = newSectionHeaderTableOffset.Value - (int)elf.Header.SectionHeaderTableFileOffset;
                            segmentData.FileSize = (ulong)((int)segmentData.FileSize + delta);
                            segmentData.MemorySize = (ulong)((int)segmentData.MemorySize + delta);
                        }
                    }
                    else
                    {
                        // We have a standard section. Check whether it has moved
                        if(movedSections.TryGetValue(lastSectionIndex, out var movedSection))
                        {
                            // It moved, so adjust the segment end address
                            segmentData.FileSize = (ulong)((int)segmentData.FileSize + movedSection.delta);
                            segmentData.MemorySize = (ulong)((int)segmentData.MemorySize + movedSection.delta);
                        }
                    }
                }

                elf.ProgramHeaderTable.ProgramHeaders[i] = segmentData;
            }

            /* Update ELF header, if necessary */

            if(newSectionHeaderTableOffset != null)
            {
                elf.Header.SectionHeaderTableFileOffset = (ulong)newSectionHeaderTableOffset.Value;
            }


            /* Update section headers */

            foreach(var movedSection in movedSections)
            {
                if(movedSection.Key < 0 || elf.SectionHeaderTable.SectionHeaders.Count <= movedSection.Key)
                    continue;

                var sectionHeader = elf.SectionHeaderTable.SectionHeaders[movedSection.Key];
                sectionHeader.FileOffset = (ulong)((int)sectionHeader.FileOffset + movedSection.Value.delta);

                // Only touch address if this section ends up in at least one non-LOAD segment
                if(elf.ProgramHeaderTable.ProgramHeaders.Any(ph => ph.Type != SegmentType.Load && ph.FileOffset <= sectionHeader.FileOffset && sectionHeader.FileOffset < ph.FileOffset + ph.FileSize))
                    sectionHeader.VirtualAddress = (ulong)((int)sectionHeader.VirtualAddress + movedSection.Value.delta);
            }

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
            if(elf.DynamicTable != null)
            {
                foreach(var entry in elf.DynamicTable.Entries)
                {
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
                            entry.Value = (ulong)GetMovedSectionBaseOffset((int)entry.Value);
                            break;
                        }

                        // TODO Adjust segment offsets
                    }
                }
            }


            /* Merge consecutive dummy chunks */

            CleanUpDummyChunks(elf);

            imageRenderer?.Invoke(elf.Chunks, $"chunks{(_imageIndex++):D3}.png");
        }

        /// <summary>
        /// Inserts new strings into the given string table section and returns the offsets of the inserted strings.
        /// </summary>
        /// <param name="elf">ELF file.</param>
        /// <param name="sectionIndex">Section index of the string table.</param>
        /// <param name="newStrings">String(s) to insert.</param>
        /// <param name="imageRenderer">(For debugging) Callback for rendering the chunk map as an image.</param>
        /// <returns>The offsets of the newly inserted strings in the string table, in order.</returns>
        /// <remarks>The string table can only grow if there is sufficient dummy chunk space behind it.</remarks>
        public static int[] ExtendStringTable(this ElfFile elf, int sectionIndex, string[] newStrings, Action<IList<Chunk>, string>? imageRenderer)
        {
            imageRenderer?.Invoke(elf.Chunks, $"chunks{(_imageIndex++):D3}.png");

            // Ensure that there are no consecutive dummy chunks (this way we always only need to deal with a single chunk)
            CleanUpDummyChunks(elf);

            var stringTableSectionHeader = elf.SectionHeaderTable.SectionHeaders[sectionIndex];
            var stringTableChunkIndex = GetChunkIndexForOffset(elf, stringTableSectionHeader.FileOffset);
            int newStringsLength = newStrings.Sum(s => s.Length + 1);

            if(stringTableChunkIndex == null || elf.Chunks[stringTableChunkIndex.Value.chunkIndex] is not StringTableChunk stringTableChunk)
                throw new Exception("Could not resolve section index to string table chunk.");

            // Check whether there is enough space
            int dummyChunkIndex = stringTableChunkIndex.Value.chunkIndex + 1;
            if(dummyChunkIndex >= elf.Chunks.Count || elf.Chunks[dummyChunkIndex] is not DummyChunk dummyChunk || dummyChunk.ByteLength < newStringsLength)
                throw new Exception("Could not find sufficient dummy chunk space behind the existing string table section.");

            // Shrink dummy chunk
            dummyChunk.Data = dummyChunk.Data.Take(dummyChunk.ByteLength - newStringsLength).ToArray();

            // Extend string table
            List<char> extendedStringArray = stringTableChunk.Data.ToList();

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

            stringTableChunk.Data = extendedStringArray.ToArray();

            // Update section header table
            stringTableSectionHeader.Size = (ulong)((int)stringTableSectionHeader.Size + newStringsLength);

            imageRenderer?.Invoke(elf.Chunks, $"chunks{(_imageIndex++):D3}.png");

            return newStringsOffsets.ToArray();
        }

        /// <summary>
        /// Inserts new symbols into the given symbol table section.
        /// </summary>
        /// <param name="elf">ELF file.</param>
        /// <param name="sectionIndex">Section index of the symbol table.</param>
        /// <param name="targetSectionIndex">Section index of the code/data the symbols are pointing to.</param>
        /// <param name="newSymbols">Symbols to insert.</param>
        /// <param name="imageRenderer">(For debugging) Callback for rendering the chunk map as an image.</param>
        /// <remarks>The symbol table can only grow if there is sufficient dummy chunk space behind it.</remarks>
        public static void ExtendSymbolTable(this ElfFile elf, int sectionIndex, int targetSectionIndex, List<(ulong offset, uint stringTableIndex)> newSymbols, Action<IList<Chunk>, string>? imageRenderer)
        {
            imageRenderer?.Invoke(elf.Chunks, $"chunks{(_imageIndex++):D3}.png");

            // Ensure that there are no consecutive dummy chunks (this way we always only need to deal with a single chunk)
            CleanUpDummyChunks(elf);

            var symbolTableSectionHeader = elf.SectionHeaderTable.SectionHeaders[sectionIndex];
            var symbolTableChunkIndex = GetChunkIndexForOffset(elf, symbolTableSectionHeader.FileOffset);
            int newSymbolEntriesLength = newSymbols.Count * (int)symbolTableSectionHeader.EntrySize;

            if(symbolTableChunkIndex == null || elf.Chunks[symbolTableChunkIndex.Value.chunkIndex] is not SymbolTableChunk symbolTableChunk)
                throw new Exception("Could not resolve section index to symbol table chunk.");

            // Check whether there is enough space
            int dummyChunkIndex = symbolTableChunkIndex.Value.chunkIndex + 1;
            if(dummyChunkIndex >= elf.Chunks.Count || elf.Chunks[dummyChunkIndex] is not DummyChunk dummyChunk || dummyChunk.ByteLength < newSymbolEntriesLength)
                throw new Exception("Could not find sufficient dummy chunk space behind the existing symbol table section.");

            // Shrink dummy chunk
            dummyChunk.Data = dummyChunk.Data.Take(dummyChunk.ByteLength - newSymbolEntriesLength).ToArray();

            // Extend symbol table
            // We have to insert local symbols before the global ones; pick an index before the last local one
            int insertionIndex = symbolTableChunk.Entries.FindLastIndex(s => (s.Info & SymbolInfo.MaskBind) == SymbolInfo.BindLocal);
            foreach(var symbol in newSymbols)
            {
                symbolTableChunk.Entries.Insert(insertionIndex++, new SymbolTableChunk.SymbolTableEntry
                {
                    Name = symbol.stringTableIndex,
                    Value = symbol.offset,
                    Size = 0,
                    Info = SymbolInfo.TypeFunc | SymbolInfo.BindLocal,
                    Visibility = SymbolVisibility.Default,
                    Section = (ushort)targetSectionIndex
                });
            }

            // Update section header table
            symbolTableSectionHeader.Size = (ulong)((int)symbolTableSectionHeader.Size + newSymbolEntriesLength);
            symbolTableSectionHeader.Info += (uint)newSymbols.Count;

            imageRenderer?.Invoke(elf.Chunks, $"chunks{(_imageIndex++):D3}.png");
        }

        /// <summary>
        /// Extends the given section by the given bytes.
        /// </summary>
        /// <param name="elf">ELF file.</param>
        /// <param name="sectionIndex">Section index.</param>
        /// <param name="bytes">Bytes to add at the end.</param>
        /// <param name="imageRenderer">(For debugging) Callback for rendering the chunk map as an image.</param>
        /// <remarks>The section can only grow if there is sufficient dummy chunk space behind it.</remarks>
        public static void ExtendRawSection(this ElfFile elf, int sectionIndex, byte[] bytes, Action<IList<Chunk>, string>? imageRenderer)
        {
            imageRenderer?.Invoke(elf.Chunks, $"chunks{(_imageIndex++):D3}.png");

            // Ensure that there are no consecutive dummy chunks (this way we always only need to deal with a single chunk)
            CleanUpDummyChunks(elf);

            var sectionHeader = elf.SectionHeaderTable.SectionHeaders[sectionIndex];
            var sectionChunkIndex = GetChunkIndexForOffset(elf, sectionHeader.FileOffset);

            if(sectionChunkIndex == null || elf.Chunks[sectionChunkIndex.Value.chunkIndex] is not RawSectionChunk sectionChunk)
                throw new Exception("Could not resolve section index to section chunk.");

            // Check whether there is enough space
            int dummyChunkIndex = sectionChunkIndex.Value.chunkIndex + 1;
            if(dummyChunkIndex >= elf.Chunks.Count || elf.Chunks[dummyChunkIndex] is not DummyChunk dummyChunk || dummyChunk.ByteLength < bytes.Length)
                throw new Exception("Could not find sufficient dummy chunk space behind the existing section.");

            // Shrink dummy chunk
            dummyChunk.Data = dummyChunk.Data.Take(dummyChunk.ByteLength - bytes.Length).ToArray();

            // Extend section
            sectionChunk.Data = sectionChunk.Data.Concat(bytes).ToArray();

            // Update section header table
            sectionHeader.Size = (ulong)((int)sectionHeader.Size + bytes.Length);

            imageRenderer?.Invoke(elf.Chunks, $"chunks{(_imageIndex++):D3}.png");
        }

        /// <summary>
        /// Creates a new section based on the given new section header table entry.
        /// </summary>
        /// <param name="elf">ELF file.</param>
        /// <param name="newSectionHeaderTableEntry">The entry to add to the section header table.</param>
        /// <param name="imageRenderer">(For debugging) Callback for rendering the chunk map as an image.</param>
        /// <returns>Index of the newly created section.</returns>
        /// <remarks>Both the space needed for the new section and the section header table must have been pre-allocated as dummy chunks.</remarks>
        public static int CreateSection(this ElfFile elf, SectionHeaderTableChunk.SectionHeaderTableEntry newSectionHeaderTableEntry, Action<IList<Chunk>, string>? imageRenderer)
        {
            imageRenderer?.Invoke(elf.Chunks, $"chunks{(_imageIndex++):D3}.png");

            // Ensure that there are no consecutive dummy chunks (this way we always only need to deal with a single chunk)
            CleanUpDummyChunks(elf);

            int sectionHeaderTableChunkIndex = elf.Chunks.FindIndex(c => c is SectionHeaderTableChunk);
            if(sectionHeaderTableChunkIndex == -1)
                throw new Exception("Could not find section header table chunk.");

            // Check whether there is enough space in the section header table
            int sectionHeaderTableDummyChunkIndex = sectionHeaderTableChunkIndex + 1;
            if(sectionHeaderTableDummyChunkIndex >= elf.Chunks.Count || elf.Chunks[sectionHeaderTableDummyChunkIndex] is not DummyChunk sectionHeaderTableDummyChunk || sectionHeaderTableDummyChunk.ByteLength < elf.SectionHeaderTable.EntrySize)
                throw new Exception("Could not find sufficient dummy chunk space behind the existing section header table.");

            // Compute space needed for the new section
            var chunkAtSectionOffset = GetChunkIndexForOffset(elf, newSectionHeaderTableEntry.FileOffset);
            if(chunkAtSectionOffset == null || elf.Chunks[chunkAtSectionOffset.Value.chunkIndex] is not DummyChunk sectionDummyChunk)
                throw new Exception("Could not find a dummy chunk at the desired offset.");
            int newSectionOffset = (int)newSectionHeaderTableEntry.FileOffset;
            int newSectionSize = (int)newSectionHeaderTableEntry.Size;
            int gapBytes = newSectionOffset - (int)chunkAtSectionOffset.Value.chunkBaseOffset;
            if(sectionDummyChunk.ByteLength < gapBytes + newSectionSize)
                throw new Exception("The dummy chunk at the given offset is too small to fit the section with the requested alignment.");
            if(chunkAtSectionOffset.Value.chunkIndex == sectionHeaderTableDummyChunkIndex && sectionDummyChunk.ByteLength < elf.SectionHeaderTable.EntrySize + newSectionSize)
                throw new Exception("The dummy chunk at the given offset is too small to fit a section header table entry and the section with the requested alignment.");

            // Create section chunk
            // sectionDummyChunk -> gap | newSection | gap
            int pos = chunkAtSectionOffset.Value.chunkIndex;
            elf.Chunks.RemoveAt(pos);
            if(gapBytes > 0)
                elf.Chunks.Insert(pos++, new DummyChunk { Data = sectionDummyChunk.Data.Take(gapBytes).ToArray() });
            elf.Chunks.Insert(pos++, new RawSectionChunk { Data = sectionDummyChunk.Data.Skip(gapBytes).Take(newSectionSize).ToArray() });
            if(sectionDummyChunk.ByteLength > gapBytes + newSectionSize)
                elf.Chunks.Insert(pos++, new DummyChunk { Data = sectionDummyChunk.Data.Skip(gapBytes + newSectionSize).ToArray() });

            // Fix chunk index of section header table
            if(sectionHeaderTableChunkIndex > chunkAtSectionOffset.Value.chunkIndex)
            {
                sectionHeaderTableDummyChunkIndex += pos - 1 - chunkAtSectionOffset.Value.chunkIndex;
            }

            // Find appropriate section index for new table entry
            int newSectionIndex = 0;
            while(newSectionIndex < elf.SectionHeaderTable.SectionHeaders.Count)
            {
                var currentSectionTableHeader = elf.SectionHeaderTable.SectionHeaders[newSectionIndex];
                if(newSectionHeaderTableEntry.FileOffset < currentSectionTableHeader.FileOffset)
                    break;

                ++newSectionIndex;
            }

            // Insert new entry
            elf.SectionHeaderTable.SectionHeaders.Insert(newSectionIndex, newSectionHeaderTableEntry);

            // Fix index of section header string table in ELF header
            int sectionHeaderStringTableIndex = elf.Header.SectionHeaderStringTableIndex;
            if(newSectionIndex <= elf.Header.SectionHeaderStringTableIndex)
                ++sectionHeaderStringTableIndex;

            // Update ELF header
            elf.Header.SectionHeaderStringTableIndex = (ushort)sectionHeaderStringTableIndex;
            elf.Header.SectionHeaderTableEntryCount = (ushort)elf.SectionHeaderTable.SectionHeaders.Count;

            // Save section header table chunks
            sectionHeaderTableDummyChunk = (DummyChunk)elf.Chunks[sectionHeaderTableDummyChunkIndex]; // The dummy chunk may be the same as the one used for allocating the new section, so ensure we have an up-to-date version
            ((DummyChunk)elf.Chunks[sectionHeaderTableDummyChunkIndex]).Data = sectionHeaderTableDummyChunk.Data.Take(sectionHeaderTableDummyChunk.ByteLength - elf.SectionHeaderTable.EntrySize).ToArray();

            imageRenderer?.Invoke(elf.Chunks, $"chunks{(_imageIndex++):D3}.png");

            return newSectionIndex;
        }

        /// <summary>
        /// Inserts a new entry into the program header table.
        /// </summary>
        /// <param name="elf">ELF file.</param>
        /// <param name="newProgramHeaderTableEntry">The new program header table entry.</param>
        /// <param name="imageRenderer">(For debugging) Callback for rendering the chunk map as an image.</param>
        /// <remarks>The table can only grow if there is sufficient dummy chunk space behind it.</remarks>
        public static void ExtendProgramHeaderTable(this ElfFile elf, ProgramHeaderTableChunk.ProgramHeaderTableEntry newProgramHeaderTableEntry, Action<IList<Chunk>, string>? imageRenderer)
        {
            imageRenderer?.Invoke(elf.Chunks, $"chunks{(_imageIndex++):D3}.png");

            // Ensure that there are no consecutive dummy chunks (this way we always only need to deal with a single chunk)
            CleanUpDummyChunks(elf);

            const int programHeaderTableChunkIndex = 1;

            // Check whether there is enough space
            int dummyChunkIndex = programHeaderTableChunkIndex + 1;
            if(dummyChunkIndex >= elf.Chunks.Count || elf.Chunks[dummyChunkIndex] is not DummyChunk dummyChunk || dummyChunk.ByteLength < elf.ProgramHeaderTable!.EntrySize)
                throw new Exception("Could not find sufficient dummy chunk space behind the existing table chunk.");

            // Shrink dummy chunk
            dummyChunk.Data = dummyChunk.Data.Take(dummyChunk.ByteLength - elf.ProgramHeaderTable.EntrySize).ToArray();

            // Find appropriate index for new table entry
            // We first look for entries with the same type, then insert such that the virtual memory addresses are in increasing order
            int insertionIndex = 0;
            for(; insertionIndex < elf.ProgramHeaderTable.ProgramHeaders.Count; ++insertionIndex)
                if(elf.ProgramHeaderTable.ProgramHeaders[insertionIndex].Type == newProgramHeaderTableEntry.Type)
                    break;
            while(insertionIndex < elf.ProgramHeaderTable.ProgramHeaders.Count)
            {
                var currentProgramTableHeader = elf.ProgramHeaderTable.ProgramHeaders[insertionIndex];
                if(newProgramHeaderTableEntry.VirtualMemoryAddress < currentProgramTableHeader.VirtualMemoryAddress || currentProgramTableHeader.Type != newProgramHeaderTableEntry.Type)
                    break;

                ++insertionIndex;
            }

            // Insert new entry
            elf.ProgramHeaderTable.ProgramHeaders.Insert(insertionIndex, newProgramHeaderTableEntry);

            // Update ELF header
            elf.Header.ProgramHeaderTableEntryCount = (ushort)elf.ProgramHeaderTable.ProgramHeaders.Count;

            imageRenderer?.Invoke(elf.Chunks, $"chunks{(_imageIndex++):D3}.png");
        }

        public static void PatchValueInRelocationTable(this ElfFile elf, ulong offset, long oldValue, long newValue)
        {
            // Find relocation table
            foreach(var chunk in elf.Chunks)
            {
                if(chunk is not RelocationAddendTableChunk relocationTableChunk)
                    continue;

                foreach(var relocationEntry in relocationTableChunk.Entries)
                {
                    if(relocationEntry.Offset == offset && relocationEntry.Addend == oldValue)
                    {
                        relocationEntry.Addend = newValue;
                    }
                }
            }
        }

        /// <summary>
        /// Reads bytes from the given file offset.
        /// The accessed bytes must reside in a raw section chunk.
        /// </summary>
        /// <param name="elf">ELF file.</param>
        /// <param name="offset">Offset where the bytes should be read.</param>
        /// <param name="bytes">Buffer for the read bytes.</param>
        public static void GetRawBytesAtOffset(this ElfFile elf, int offset, Span<byte> bytes)
        {
            // Resolve chunk
            (int chunkIndex, ulong chunkBaseOffset) = GetChunkIndexForOffset(elf, (ulong)offset)
                                                      ?? throw new Exception("Could not locate chunk belonging to the given offset.");
            if(elf.Chunks[chunkIndex] is not RawSectionChunk rawSectionChunk)
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
        /// <param name="elf">ELF file.</param>
        /// <param name="offset">Offset where the bytes should be replaced.</param>
        /// <param name="newBytes">New bytes.</param>
        public static void PatchRawBytesAtOffset(this ElfFile elf, int offset, ReadOnlySpan<byte> newBytes)
        {
            // Resolve chunk
            (int chunkIndex, ulong chunkBaseOffset) = GetChunkIndexForOffset(elf, (ulong)offset)
                                                      ?? throw new Exception("Could not locate chunk belonging to the given offset.");
            if(elf.Chunks[chunkIndex] is not RawSectionChunk rawSectionChunk)
                throw new InvalidOperationException("This method can only patch raw section chunks.");

            // Patch chunk data
            int relativeChunkOffset = offset - (int)chunkBaseOffset;
            for(int i = 0; i < newBytes.Length; ++i)
                rawSectionChunk.Data[relativeChunkOffset + i] = newBytes[i];
        }

        /// <summary>
        /// Replaces a number of bytes at the given virtual address, as determined by the program header table.
        /// The replaced bytes must reside in a raw section chunk.
        /// </summary>
        /// <param name="elf">ELF file.</param>
        /// <param name="address">Virtual address where the bytes should be replaced.</param>
        /// <param name="newBytes">New bytes.</param>
        public static void PatchRawBytesAtAddress(this ElfFile elf, int address, ReadOnlySpan<byte> newBytes)
        {
            // Resolve segment
            int endAddress = address + newBytes.Length;
            var programHeaderTableEntry = elf.ProgramHeaderTable!.ProgramHeaders.First(ph => (int)ph.VirtualMemoryAddress <= address && endAddress <= (int)(ph.VirtualMemoryAddress + ph.FileSize));

            // Resolve chunk
            int relativeSegmentOffset = address - (int)programHeaderTableEntry.VirtualMemoryAddress;
            (int chunkIndex, ulong chunkBaseOffset) = GetChunkIndexForOffset(elf, programHeaderTableEntry.FileOffset + (ulong)relativeSegmentOffset)
                                                      ?? throw new Exception("Could not locate chunk belonging to the given offset.");
            if(elf.Chunks[chunkIndex] is not RawSectionChunk rawSectionChunk)
                throw new InvalidOperationException("This method can only patch raw section chunks.");

            // Patch chunk data
            int relativeChunkOffset = (int)programHeaderTableEntry.FileOffset + relativeSegmentOffset - (int)chunkBaseOffset;
            for(int i = 0; i < newBytes.Length; ++i)
                rawSectionChunk.Data[relativeChunkOffset + i] = newBytes[i];
        }

        /// <summary>
        /// Merges consecutive dummy chunks and removes empty ones.
        /// </summary>
        /// <param name="elf">ELF file.</param>
        public static void CleanUpDummyChunks(this ElfFile elf)
        {
            for(int i = 0; i < elf.Chunks.Count;)
            {
                if(elf.Chunks[i] is not DummyChunk dummyChunk)
                {
                    ++i;
                    continue;
                }

                // Look for more dummy chunks
                while(i + 1 < elf.Chunks.Count && elf.Chunks[i + 1] is DummyChunk anotherDummyChunk)
                {
                    // Merge chunks
                    dummyChunk.Data = dummyChunk.Data.Concat(anotherDummyChunk.Data).ToArray();
                    elf.Chunks.RemoveAt(i + 1);
                }

                // If the dummy chunk is empty, remove it altogether
                if(dummyChunk.ByteLength == 0)
                    elf.Chunks.RemoveAt(i);
                else
                    ++i;
            }
        }

        /// <summary>
        /// Maps the given file offset to the corresponding chunk index.
        /// </summary>
        /// <param name="elf">ELF file.</param>
        /// <param name="offset">File offset (may point to any position in a chunk).</param>
        /// <returns>A tuple consisting of the chunk index and the chunk's base file offset. If the corresponding chunk cannot be located, this method returns null.</returns>
        public static (int chunkIndex, ulong chunkBaseOffset)? GetChunkIndexForOffset(this ElfFile elf, ulong offset)
        {
            // Find chunk
            // The chunk list is ordered by offset, so we can just traverse it
            int address = 0;
            for(var index = 0; index < elf.Chunks.Count; index++)
            {
                var chunk = elf.Chunks[index];

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