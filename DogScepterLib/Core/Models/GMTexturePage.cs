using ICSharpCode.SharpZipLib.BZip2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DogScepterLib.Core.Models
{
    /// <summary>
    /// Contains a GameMaker texture page.
    /// </summary>
    public class GMTexturePage : GMSerializable
    {
        public uint Scaled;
        public uint GeneratedMips;
        public GMTextureData TextureData;

        public void Serialize(GMDataWriter writer)
        {
            writer.Write(Scaled);
            if (writer.VersionInfo.Major >= 2) 
                writer.Write(GeneratedMips);
            writer.WritePointer(TextureData);
        }

        public void Unserialize(GMDataReader reader)
        {
            Scaled = reader.ReadUInt32();
            if (reader.VersionInfo.Major >= 2) 
                GeneratedMips = reader.ReadUInt32();
            TextureData = reader.ReadPointerObjectUnique<GMTextureData>();
        }
    }

    public class GMTextureData : GMSerializable
    {
        // The PNG or QOI+BZip2 data
        public BufferRegion Data;

        // Fields specifically for QOI *only*
        public bool IsQoi;
        public bool IsBZip2;
        public short QoiWidth = -1;
        public short QoiHeight = -1;

        private static readonly byte[] PNGHeader = new byte[8] { 137, 80, 78, 71, 13, 10, 26, 10 };
        private static readonly byte[] QOIandBZip2Header = new byte[4] { 50, 122, 111, 113 };
        private static readonly byte[] QOIHeader = new byte[4] { 102, 105, 111, 113 };

        public void Serialize(GMDataWriter writer)
        {
            writer.Pad(128);
            writer.WriteObjectPointer(this);
            if (IsQoi && IsBZip2)
            {
                // Need to compress the data now
                writer.Write(QOIandBZip2Header);
                writer.Write(QoiWidth);
                writer.Write(QoiHeight);
                using MemoryStream input = new MemoryStream(Data.Memory.ToArray());
                using MemoryStream output = new MemoryStream(1024);
                BZip2.Compress(input, output, false, 9);
                writer.Write(output.ToArray());
            }
            else
                writer.Write(Data);
        }

        public void Unserialize(GMDataReader reader)
        {
            int startOffset = reader.Offset;

            byte[] header = reader.ReadBytes(8).Memory.ToArray();
            if (!header.SequenceEqual(PNGHeader))
            {
                reader.Offset = startOffset;
                if (header.Take(4).SequenceEqual(QOIandBZip2Header))
                {
                    // This is in QOI + BZip2 format
                    IsQoi = true;
                    IsBZip2 = true;
                    reader.VersionInfo.SetNumber(2022, 1);
                    reader.Offset += 4;

                    QoiWidth = reader.ReadInt16();
                    QoiHeight = reader.ReadInt16();

                    // Queue the data to be decompressed later, and in parallel
                    reader.TexturesToDecompress.Add((this, reader.Offset));
                    return;
                }
                else if (header.Take(4).SequenceEqual(QOIHeader))
                {
                    // This is in QOI format
                    IsQoi = true;
                    reader.VersionInfo.SetNumber(2022, 1);

                    int dataStart = reader.Offset;

                    reader.Offset += 8; // skip header and Width/Height, not needed
                    int length = reader.ReadInt32();

                    reader.Offset = dataStart;
                    Data = reader.ReadBytes(length + 12);
                    return;
                }
                else
                    reader.Warnings.Add(new GMWarning("PNG, QOI, or QOI+BZ2 header expected.", GMWarning.WarningLevel.Bad));
            }

            // Parse PNG data
            int type;
            do
            {
                uint length = (uint)reader.ReadByte() << 24 | (uint)reader.ReadByte() << 16 | (uint)reader.ReadByte() << 8 | (uint)reader.ReadByte();
                type = reader.ReadInt32();
                reader.Offset += (int)length + 4;
            }
            while (type != 0x444E4549 /* IEND */);

            int texLength = reader.Offset - startOffset;
            reader.Offset = startOffset;
            Data = reader.ReadBytes(texLength);
        }
    }
}
