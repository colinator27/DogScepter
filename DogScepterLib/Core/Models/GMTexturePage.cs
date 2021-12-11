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
        public short QoiWidth = -1;
        public short QoiHeight = -1;

        public void Serialize(GMDataWriter writer)
        {
            writer.Pad(128);
            writer.WriteObjectPointer(this);
            if (IsQoi)
            {
                writer.Write(new byte[] { 50, 122, 111, 113 });
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
            if (!header.SequenceEqual(new byte[8] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            {
                reader.Offset = startOffset;
                if (header.Take(4).SequenceEqual(new byte[4] { 50, 122, 111, 113 }))
                {
                    // This is in QOI + BZip2 format
                    IsQoi = true;
                    reader.VersionInfo.SetNumber(2, 3, 8);
                    reader.VersionInfo.UseQoiFormat = true;
                    reader.Offset += 4;

                    QoiWidth = reader.ReadInt16();
                    QoiHeight = reader.ReadInt16();

                    // Decompress BZip2 data, leaving just QOI data
                    using MemoryStream bufferWrapper = new MemoryStream(reader.Buffer);
                    bufferWrapper.Seek(reader.Offset, SeekOrigin.Begin);
                    using MemoryStream result = new MemoryStream(1024);
                    BZip2.Decompress(bufferWrapper, result, false);
                    Data = new BufferRegion(result.ToArray());
                    return;
                }
                else
                    reader.Warnings.Add(new GMWarning("PNG or QOI+BZip2 header expected.", GMWarning.WarningLevel.Bad));
            }

            while (true)
            {
                uint length = (uint)reader.ReadByte() << 24 | (uint)reader.ReadByte() << 16 | (uint)reader.ReadByte() << 8 | (uint)reader.ReadByte();
                string type = reader.ReadChars(4);
                reader.Offset += (int)length + 4;
                if (type == "IEND")
                    break;
            }

            int texLength = reader.Offset - startOffset;
            reader.Offset = startOffset;
            Data = reader.ReadBytes(texLength);
        }
    }
}
