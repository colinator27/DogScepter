using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Models
{
    public class GMSprite : GMSerializable
    {
        public GMString Name;
        public int Width;
        public int Height;
        public int MarginLeft;
        public int MarginRight;
        public int MarginBottom;
        public int MarginTop;
        public bool Transparent;
        public bool Smooth;
        public bool Preload;
        public uint BBoxMode;
        public SepMaskType SepMasks;
        public int OriginX;
        public int OriginY;
        public bool SpecialOrGMS2 = false;

        public GMRemotePointerList<GMTextureItem> TextureItems;
        public List<byte[]> CollisionMasks;

        public enum SepMaskType
        {
            AxisAlignedRect = 0,
            Precise = 1,
            RotatedRect = 2
        }

        public void Serialize(GMDataWriter writer)
        {
            writer.WritePointerString(Name);
            writer.Write(Width);
            writer.Write(Height);
            writer.Write(MarginLeft);
            writer.Write(MarginRight);
            writer.Write(MarginBottom);
            writer.Write(MarginTop);
            writer.WriteWideBoolean(Transparent);
            writer.WriteWideBoolean(Smooth);
            writer.WriteWideBoolean(Preload);
            writer.Write(BBoxMode);
            writer.Write((int)SepMasks);
            writer.Write(OriginX);
            writer.Write(OriginY);

            if (SpecialOrGMS2)
            {
                // TODO
            }
            else
            {
                TextureItems.Serialize(writer);
                WriteMaskData(writer);
            }
        }

        public void Unserialize(GMDataReader reader)
        {
            Name = reader.ReadStringPointerObject();
            Width = reader.ReadInt32();
            Height = reader.ReadInt32();
            MarginLeft = reader.ReadInt32();
            MarginRight = reader.ReadInt32();
            MarginBottom = reader.ReadInt32();
            MarginTop = reader.ReadInt32();
            Transparent = reader.ReadWideBoolean();
            Smooth = reader.ReadWideBoolean();
            Preload = reader.ReadWideBoolean();
            BBoxMode = reader.ReadUInt32();
            SepMasks = (SepMaskType)reader.ReadInt32();
            OriginX = reader.ReadInt32();
            OriginY = reader.ReadInt32();

            TextureItems = new GMRemotePointerList<GMTextureItem>();
            if (reader.ReadInt32() == -1)
            {
                // Special/GMS2 sprite type
                SpecialOrGMS2 = true;

            } else
            {
                // Normal, GM:S 1.4 sprite
                reader.Offset -= 4;
                TextureItems.Unserialize(reader);
                ParseMaskData(reader);
            }
        }

        private void ParseMaskData(GMDataReader reader)
        {
            int MaskCount = reader.ReadInt32();
            int len = (Width + 7) / 8 * Height;

            CollisionMasks = new List<byte[]>();
            int total = 0;
            for (uint i = 0; i < MaskCount; i++)
            {
                CollisionMasks.Add(reader.ReadBytes(len));
                total += len;
            }

            // Pad to 4 bytes
            if (total % 4 != 0)
                total += 4 - (total % 4);

            int totalBits = ((Width + 7) / 8 * 8) * Height * MaskCount;
            int totalBytes = ((totalBits + 31) / 32 * 32) / 8;
            if (total != totalBytes)
                reader.Warnings.Add(new GMWarning("Unexpected sprite mask length!"));
        }

        private void WriteMaskData(GMDataWriter writer)
        {
            writer.Write(CollisionMasks.Count);
            int total = 0;
            foreach (var mask in CollisionMasks)
            {
                writer.Write(mask);
                total += mask.Length;
            }

            // Pad to 4 bytes
            if (total % 4 != 0)
                total += 4 - (total % 4);
            writer.Pad(4);

            int totalBits = ((Width + 7) / 8 * 8) * Height * CollisionMasks.Count;
            int totalBytes = ((totalBits + 31) / 32 * 32) / 8;
            if (total != totalBytes)
                writer.Warnings.Add(new GMWarning("Unexpected sprite mask length!"));
        }
    }
}
