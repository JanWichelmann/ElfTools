using System;
using System.IO;

namespace ElfTools
{
    /// <summary>
    /// Provides a simple interface for iterating over an ELF file's contents.
    /// Intended for use with disassembly methods.
    /// </summary>
    public sealed class ElfChunkStream : Stream
    {
        private readonly ElfFile _elfFile;
        private int _currentChunkIndex;
        private long _currentPositionInChunk;
        private byte[] _currentChunkData;
        
        /// <summary>
        /// Creates a new stream from the given ELF file.
        /// </summary>
        /// <param name="elfFile">ELF file.</param>
        public ElfChunkStream(ElfFile elfFile)
        {
            _elfFile = elfFile ?? throw new ArgumentNullException(nameof(elfFile));
            Length = elfFile.GetByteLength();

            _currentChunkIndex = 0;
            _currentChunkData = elfFile.Chunks[_currentChunkIndex].Bytes;
            _currentPositionInChunk = 0;
            Position = 0;
        }

        /// <summary>
        /// Reads up to <see cref="count"/> bytes from the current chunk and returns the number of read bytes.
        /// If the chunk end is hit, the next chunk is loaded.
        /// </summary>
        /// <param name="buffer">Target buffer.</param>
        /// <param name="count">The maximum amount of bytes to read.</param>
        /// <returns>Number of read bytes.</returns>
        private int ReadDataFromCurrentChunk(Span<byte> buffer, int count)
        {
            // Try to fetch the next chunk, if necessary
            if(_currentPositionInChunk == _currentChunkData.Length && count > 0)
            {
                if(_currentChunkIndex >= _elfFile.Chunks.Count - 1)
                    return 0;
                
                ++_currentChunkIndex;
                _currentChunkData = _elfFile.Chunks[_currentChunkIndex].Bytes;
                _currentPositionInChunk = 0;
            }
            
            // Are there bytes left in the current chunk?
            int bytesRead = 0;
            if(_currentPositionInChunk < _currentChunkData.Length)
            {
                bytesRead = (int)Math.Min(_currentChunkData.Length - _currentPositionInChunk, count);
                _currentChunkData.AsSpan((int)_currentPositionInChunk, bytesRead).CopyTo(buffer);
                _currentPositionInChunk += bytesRead;
                Position += bytesRead;
            }

            return bytesRead;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            // Read until the buffer is full or there are no more bytes in this stream
            int bytesRead = 0;
            var bufferSpan = buffer.AsSpan(offset);
            while(bytesRead < count && Position < Length)
            {
                bytesRead += ReadDataFromCurrentChunk(bufferSpan[bytesRead..], count - bytesRead);
            }

            return bytesRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            // Compute new offset depending on the seek origin
            offset = origin switch
            {
                SeekOrigin.Current => Position + offset,
                SeekOrigin.End => Length - offset,
                _ => offset
            };

            if(offset <= 0 || offset >= Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            
            // Find matching chunk
            // We have already done a range check, so we can assume that this operation is able to retrieve the corresponding chunk
            ulong chunkBaseOffset;
            (_currentChunkIndex, chunkBaseOffset) = _elfFile.GetChunkAtFileOffset((ulong)offset)!.Value;
            _currentPositionInChunk = offset - (long)chunkBaseOffset;
            _currentChunkData = _elfFile.Chunks[_currentChunkIndex].Bytes;
            Position = offset;
            
            return offset;
        }
        
        public override long Length { get; }
        public override long Position { get; set; }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
    }
}