using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Models
{
    /// <summary>
    /// Contains a GameMaker font.
    /// </summary>
    public class GMFont : IGMNamedSerializable
    {
        public GMString Name { get; set; }
        public GMString DisplayName;
        public int Size; // In 2.3(?), this seems to be a float instead (below value)
        public float SizeFloat;
        public bool Bold;
        public bool Italic;
        public ushort RangeStart;
        public byte Charset;
        public byte AntiAlias;
        public int RangeEnd;
        public GMTextureItem TextureItem;
        public float ScaleX, ScaleY;
        public int AscenderOffset;
        public int Ascender;
        public GMUniquePointerList<GMGlyph> Glyphs;

        public void Serialize(GMDataWriter writer)
        {
            writer.WritePointerString(Name);
            writer.WritePointerString(DisplayName);
            if (Size < 0)
                Size = BitConverter.ToInt32(BitConverter.GetBytes(-SizeFloat));
            writer.Write(Size);
            writer.WriteWideBoolean(Bold);
            writer.WriteWideBoolean(Italic);
            writer.Write(RangeStart);
            writer.Write(Charset);
            writer.Write(AntiAlias);
            writer.Write(RangeEnd);
            writer.WritePointer(TextureItem);
            writer.Write(ScaleX);
            writer.Write(ScaleY);
            if (writer.VersionInfo.FormatID >= 17)
                writer.Write(AscenderOffset);
            if (writer.VersionInfo.IsVersionAtLeast(2022, 2))
                writer.Write(Ascender);
            Glyphs.Serialize(writer);
        }

        public void Deserialize(GMDataReader reader)
        {
            Name = reader.ReadStringPointerObject();
            DisplayName = reader.ReadStringPointerObject();
            Size = reader.ReadInt32();
            if (Size < 0)
            {
                reader.Offset -= 4;
                SizeFloat = -reader.ReadSingle();
            }
            Bold = reader.ReadWideBoolean();
            Italic = reader.ReadWideBoolean();
            RangeStart = reader.ReadUInt16();
            Charset = reader.ReadByte();
            AntiAlias = reader.ReadByte();
            RangeEnd = reader.ReadInt32();
            TextureItem = reader.ReadPointer<GMTextureItem>();
            ScaleX = reader.ReadSingle();
            ScaleY = reader.ReadSingle();
            if (reader.VersionInfo.FormatID >= 17)
                AscenderOffset = reader.ReadInt32();
            if (reader.VersionInfo.IsVersionAtLeast(2022, 2))
                Ascender = reader.ReadInt32();
            Glyphs = new GMUniquePointerList<GMGlyph>();
            Glyphs.Deserialize(reader);
        }

        public override string ToString()
        {
            return $"Font: \"{Name.Content}\"";
        }
    }

    public class GMGlyph : IGMSerializable
    {
        public ushort Character { get; set; }
        public ushort X { get; set; }
        public ushort Y { get; set; }
        public ushort Width { get; set; }
        public ushort Height{ get; set; }
        public short Shift { get; set; }
        public short Offset { get; set; }
        public List<GMKerning> Kerning { get; set; }

        public void Serialize(GMDataWriter writer)
        {
            writer.Write(Character);
            writer.Write(X);
            writer.Write(Y);
            writer.Write(Width);
            writer.Write(Height);
            writer.Write(Shift);
            writer.Write(Offset);
            writer.Write((ushort)Kerning.Count);
            for (int i = 0; i < Kerning.Count; i++)
                Kerning[i].Serialize(writer);
        }

        public void Deserialize(GMDataReader reader)
        {
            Character = reader.ReadUInt16();
            X = reader.ReadUInt16();
            Y = reader.ReadUInt16();
            Width = reader.ReadUInt16();
            Height = reader.ReadUInt16();
            Shift = reader.ReadInt16();
            Offset = reader.ReadInt16();
            Kerning = new List<GMKerning>();
            for (ushort i = reader.ReadUInt16(); i > 0; i--)
            {
                GMKerning k = new GMKerning();
                k.Deserialize(reader);
                Kerning.Add(k);
            }
        }
    }

    public class GMKerning : IGMSerializable
    {
        public short Other { get; set; }
        public short Amount { get; set; }

        public void Serialize(GMDataWriter writer)
        {
            writer.Write(Other);
            writer.Write(Amount);
        }

        public void Deserialize(GMDataReader reader)
        {
            Other = reader.ReadInt16();
            Amount = reader.ReadInt16();
        }
    }
}
