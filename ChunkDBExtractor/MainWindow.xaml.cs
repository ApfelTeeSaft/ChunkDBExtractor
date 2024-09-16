using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using System.Collections.Generic;
using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace ChunkDBExtractor
{
    public partial class MainWindow : Window
    {
        private string sourceFolder;
        private string destinationFolder;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void SelectSourceButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                CheckFileExists = false,
                CheckPathExists = true,
                ValidateNames = false,
                FileName = "Select Folder"
            };

            if (dialog.ShowDialog() == true)
            {
                sourceFolder = Path.GetDirectoryName(dialog.FileName);
                SourceFolderTextBox.Text = sourceFolder;
                CheckIfReadyToExtract();
            }
        }

        private void SelectDestinationButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                CheckFileExists = false,
                CheckPathExists = true,
                ValidateNames = false,
                FileName = "Select Folder"
            };

            if (dialog.ShowDialog() == true)
            {
                destinationFolder = Path.GetDirectoryName(dialog.FileName);
                DestinationFolderTextBox.Text = destinationFolder;
                CheckIfReadyToExtract();
            }
        }

        private void ExtractButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(sourceFolder) || string.IsNullOrEmpty(destinationFolder))
            {
                MessageBox.Show("Please select both source and destination folders.");
                return;
            }

            try
            {
                ExtractChunkDBFiles(sourceFolder, destinationFolder);
                MessageBox.Show("Files extracted successfully!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}");
            }
        }

        private void CheckIfReadyToExtract()
        {
            ExtractButton.IsEnabled = !string.IsNullOrEmpty(sourceFolder) && !string.IsNullOrEmpty(destinationFolder);
        }

        private void ExtractChunkDBFiles(string source, string destination)
        {
            foreach (var file in Directory.GetFiles(source, "*.chunkdb"))
            {
                try
                {
                    using (var stream = File.OpenRead(file))
                    using (var reader = new BinaryReader(stream))
                    {
                        var chunkDatabase = new FChunkDatabase(reader);

                        foreach (var location in chunkDatabase.Locations)
                        {
                            stream.Seek((long)location.ByteStart, SeekOrigin.Begin);
                            var chunkHeader = new FChunkHeader(reader);

                            var guidString = $"{chunkHeader.Guid.A:X8}{chunkHeader.Guid.B:X8}{chunkHeader.Guid.C:X8}{chunkHeader.Guid.D:X8}";
                            var chunkFilePath = Path.Combine(destination, guidString);

                            var chunkData = reader.ReadBytes((int)chunkHeader.DataSizeCompressed);
                            var decompressedData = chunkHeader.StoredAs == EChunkStorageFlags.None ? chunkData : FChunkHeader.Decompress(chunkData);

                            File.WriteAllBytes(chunkFilePath, decompressedData);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred while extracting {file}: {ex.Message}");
                }
            }
        }

        // ChunkDBTool
        private class FChunkDatabase
        {
            private const uint CHUNKDB_HEADER_MAGIC = 0xB1FE3AA3;

            public uint Version { get; private set; }
            public uint HeaderSize { get; private set; }
            public ulong DataSize { get; private set; }
            public int ChunkCount { get; private set; }

            public FChunkLocation[] Locations { get; private set; }
            public FChunkHeader[] Chunks { get; private set; }

            public FChunkDatabase(BinaryReader reader)
            {
                if (reader.ReadUInt32() != CHUNKDB_HEADER_MAGIC)
                    throw new Exception("Incorrect chunkdb.");

                Version = reader.ReadUInt32();
                HeaderSize = reader.ReadUInt32();
                DataSize = reader.ReadUInt64();
                ChunkCount = reader.ReadInt32();

                var locations = new List<FChunkLocation>();
                for (var i = 0; i < ChunkCount; i++)
                    locations.Add(new FChunkLocation(reader));

                Locations = locations.ToArray();

                var chunks = new List<FChunkHeader>();
                for (var i = 0; i < Locations.Length; i++)
                {
                    reader.BaseStream.Position = (long)Locations[i].ByteStart;
                    chunks.Add(new FChunkHeader(reader));
                }

                Chunks = chunks.ToArray();
            }
        }

        private class FChunkLocation
        {
            public FGuid ChunkId { get; private set; }
            public ulong ByteStart { get; private set; }
            public int ByteSize { get; private set; }

            public FChunkLocation(BinaryReader reader)
            {
                ChunkId = new FGuid(reader);
                ByteStart = reader.ReadUInt64();
                ByteSize = reader.ReadInt32();
            }
        }

        private class FGuid
        {
            public uint A { get; private set; }
            public uint B { get; private set; }
            public uint C { get; private set; }
            public uint D { get; private set; }

            public FGuid(BinaryReader reader)
            {
                A = reader.ReadUInt32();
                B = reader.ReadUInt32();
                C = reader.ReadUInt32();
                D = reader.ReadUInt32();
            }
        }

        private class FChunkHeader
        {
            private const uint CHUNK_HEADER_MAGIC = 0xB1FE3AA2;

            public uint Version { get; private set; }
            public uint HeaderSize { get; private set; }
            public uint DataSizeCompressed { get; private set; }
            public FGuid Guid { get; private set; }
            public ulong RollingHash { get; private set; }
            public EChunkStorageFlags StoredAs { get; private set; }
            public byte[] SHAHash { get; private set; }
            public EChunkHashFlags HashType { get; private set; }

            public FChunkHeader(BinaryReader reader)
            {
                if (reader.ReadUInt32() != CHUNK_HEADER_MAGIC)
                    throw new Exception("Incorrect chunk.");

                Version = reader.ReadUInt32();
                HeaderSize = reader.ReadUInt32();
                DataSizeCompressed = reader.ReadUInt32();
                Guid = new FGuid(reader);
                RollingHash = reader.ReadUInt64();
                StoredAs = (EChunkStorageFlags)reader.ReadByte();
                SHAHash = reader.ReadBytes(20);
                HashType = (EChunkHashFlags)reader.ReadByte();
            }

            public static byte[] Decompress(byte[] data)
            {
                MemoryStream inflated = new MemoryStream();
                using (Stream inflater = new InflaterInputStream(
                    new MemoryStream(data), new Inflater(false)))
                {
                    int count = 0;
                    byte[] deflated = new byte[4096];
                    while ((count = inflater.Read(deflated, 0, deflated.Length)) != 0)
                    {
                        inflated.Write(deflated, 0, count);
                    }
                    inflated.Seek(0, SeekOrigin.Begin);
                }
                byte[] content = new byte[inflated.Length];
                inflated.Read(content, 0, content.Length);
                return content;
            }
        }

        [Flags]
        private enum EChunkStorageFlags : byte
        {
            None = 0,
            Compressed = 1,
            Encrypted = 2
        }

        [Flags]
        private enum EChunkHashFlags : byte
        {
            None = 0,
            RollingPoly64 = 1,
            Sha1 = 2
        }
    }
}