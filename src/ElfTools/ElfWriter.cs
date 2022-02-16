using System.IO;

namespace ElfTools
{
    /// <summary>
    /// Provides functionality to write ELF files.
    /// </summary>
    public static class ElfWriter
    {
        /// <summary>
        /// Stores an ELF file at the given path.
        /// </summary>
        /// <param name="elfFile">ELF file.</param>
        /// <param name="path">Destination path.</param>
        public static void Store(ElfFile elfFile, string path)
        {
            using var stream = File.Open(path, FileMode.Create);
            using var writer = new BinaryWriter(stream);
            Store(elfFile, writer);
        }

        /// <summary>
        /// Stores an ELF file using the given stream writer.
        /// </summary>
        /// <param name="elfFile">ELF file.</param>
        /// <param name="writer">Binary stream writer.</param>
        public static void Store(ElfFile elfFile, BinaryWriter writer)
        {
            // Write chunks
            foreach(var chunk in elfFile.Chunks)
            {
                writer.Write(chunk.Bytes);
            }
        }
    }
}