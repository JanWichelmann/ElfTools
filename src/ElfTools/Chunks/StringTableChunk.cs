using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace ElfTools.Chunks
{
    public class StringTableChunk : SectionChunk
    {
        /// <summary>
        /// Raw string table data.
        /// </summary>
        public char[] Data { get; set; }

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
                Data = data
            };
        }

        /// <summary>
        /// Greates a new chunk from the given string array.
        /// </summary>
        /// <param name="strings">Strings to insert into the new string table chunk.</param>
        /// <returns>The new string table chunk and the offsets of the respective strings, in the same order as specified in <see cref="strings"/>.</returns>
        public static (StringTableChunk chunk, int[] offsets) FromStrings(params string[] strings)
        {
            char[] data = new char[strings.Sum(s => s.Length + 1) + 1];
            int[] offsets = new int[strings.Length];

            int offset = 0;
            for(var i = 0; i < strings.Length; i++)
            {
                var str = strings[i];
                offsets[i] = offset;

                str.CopyTo(0, data, offset, str.Length);
                offset += str.Length + 1;
            }

            return (new StringTableChunk { Data = data }, offsets);
        }
    }
}