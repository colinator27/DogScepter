using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DogScepterLib.Core.Models
{
    public class GMTexturePage : GMSerializable
    {
        public uint Scaled;
        public uint GeneratedMips;
        public GMTextureData TextureData;

        public void Serialize(GMDataWriter writer)
        {
        }

        public void Unserialize(GMDataReader reader)
        {
            Scaled = reader.ReadUInt32();
            if (reader.VersionInfo.Major >= 2) 
                GeneratedMips = reader.ReadUInt32();
            reader.ReadPointerObject<GMTextureData>();
        }
    }

    public class GMTextureData : GMSerializable
    {
        // The PNG data
        public byte[] Data;

        public void Serialize(GMDataWriter writer)
        {
        }

        public void Unserialize(GMDataReader reader)
        {
            int startOffset = reader.Offset;

            if (!reader.ReadBytes(8).SequenceEqual(new byte[8] { 137, 80, 78, 71, 13, 10, 26, 10 }))
                reader.Warnings.Add(new GMWarning("PNG header expected.", GMWarning.WarningLevel.Bad));

            while (true)
            {
                uint length = (uint)reader.ReadByte() << 24 | (uint)reader.ReadByte() << 16 | (uint)reader.ReadByte() << 8 | (uint)reader.ReadByte();
                string type = reader.ReadChars(4);
                reader.ReadBytes((int)length);
                reader.Offset += 4;
                if (type == "IEND")
                    break;
            }

            int texLength = reader.Offset - startOffset;
            reader.Offset = startOffset;
            Data = reader.ReadBytes(texLength);
        }
    }
}
