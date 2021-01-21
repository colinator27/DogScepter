using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Models
{
    /// <summary>
    /// Contains a GameMaker font.
    /// </summary>
    public class GMFont : GMSerializable
    {
        public GMString Name;
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
        public GMPointerList<GMGlyph> Glyphs;

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
            Glyphs.Serialize(writer);
        }

        public void Unserialize(GMDataReader reader)
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
            Glyphs = new GMPointerList<GMGlyph>();
            Glyphs.Unserialize(reader);
        }

        public override string ToString()
        {
            return $"Font: {Name.Content}";
        }
    }

    public class GMGlyph : GMSerializable
    {
        public ushort Character;
        public ushort X, Y;
        public ushort Width, Height;
        public short Shift, Offset;
        public List<GMKerning> Kerning;

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
            for (int i = Kerning.Count; i > 0; i--)
                Kerning[i].Serialize(writer);
        }

        public void Unserialize(GMDataReader reader)
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
                k.Unserialize(reader);
                Kerning.Add(k);
            }
        }
    }

    public class GMKerning : GMSerializable
    {
        public short Other;
        public short Amount;

        public void Serialize(GMDataWriter writer)
        {
            writer.Write(Other);
            writer.Write(Amount);
        }

        public void Unserialize(GMDataReader reader)
        {
            Other = reader.ReadInt16();
            Amount = reader.ReadInt16();
        }
    }
}
