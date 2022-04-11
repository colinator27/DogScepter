using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Models
{
    /// <summary>
    /// Contains a GameMaker sprite.
    /// </summary>
    public class GMSprite : IGMNamedSerializable
    {
        public GMString Name { get; set; }
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

        public SpriteType S_SpriteType;
        public BufferRegion S_Buffer;

        public float GMS2_PlaybackSpeed;
        public AnimSpeedType GMS2_PlaybackSpeedType;

        public SequenceReference GMS2_3_Sequence;
        public NineSlice GMS2_3_2_NineSlice;

        public GMRemotePointerList<GMTextureItem> TextureItems;
        public List<BufferRegion> CollisionMasks;

        public enum SepMaskType : int
        {
            AxisAlignedRect = 0,
            Precise = 1,
            RotatedRect = 2
        }

        public enum SpriteType : int
        {
            Normal = 0,
            SWF = 1,
            Spine = 2
        }

        public enum AnimSpeedType : int
        {
            FramesPerSecond = 0,
            FramesPerGameFrame = 1
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
                // Special/GMS2 sprite type
                writer.Write(-1);
                if (writer.VersionInfo.IsVersionAtLeast(2, 3, 2))
                    writer.Write(3);
                else if (writer.VersionInfo.IsVersionAtLeast(2, 3))
                    writer.Write(2);
                else
                    writer.Write(1);
                writer.Write((int)S_SpriteType);
                if (writer.VersionInfo.IsVersionAtLeast(2))
                {
                    writer.Write(GMS2_PlaybackSpeed);
                    writer.Write((int)GMS2_PlaybackSpeedType);
                    if (writer.VersionInfo.IsVersionAtLeast(2, 3))
                    {
                        writer.WritePointer(GMS2_3_Sequence);
                        if (writer.VersionInfo.IsVersionAtLeast(2, 3, 2))
                        {
                            writer.WritePointer(GMS2_3_2_NineSlice);
                        }
                    }
                }

                switch (S_SpriteType)
                {
                    case SpriteType.Normal:
                        TextureItems.Serialize(writer);
                        WriteMaskData(writer);
                        break;
                    case SpriteType.SWF:
                        writer.Write(8);
                        TextureItems.Serialize(writer);
                        writer.Pad(4);
                        writer.Write(S_Buffer);
                        break;
                    case SpriteType.Spine:
                        writer.Pad(4);
                        writer.Write(S_Buffer);
                        break;
                }

                if (GMS2_3_Sequence != null)
                {
                    writer.Pad(4);
                    writer.WriteObjectPointer(GMS2_3_Sequence);
                    GMS2_3_Sequence.Serialize(writer);
                }

                if (GMS2_3_2_NineSlice != null)
                {
                    writer.Pad(4);
                    writer.WriteObjectPointer(GMS2_3_2_NineSlice);
                    GMS2_3_2_NineSlice.Serialize(writer);
                }
            }
            else
            {
                // Normal sprite type
                TextureItems.Serialize(writer);
                WriteMaskData(writer);
            }
        }

        public void Deserialize(GMDataReader reader)
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

                int version = reader.ReadInt32();
                S_SpriteType = (SpriteType)reader.ReadInt32();
                if (reader.VersionInfo.IsVersionAtLeast(2))
                {
                    GMS2_PlaybackSpeed = reader.ReadSingle();
                    GMS2_PlaybackSpeedType = (AnimSpeedType)reader.ReadInt32();
                    if (version >= 2)
                    {
                        GMS2_3_Sequence = reader.ReadPointerObjectUnique<SequenceReference>();
                        if (version >= 3)
                        {
                            reader.VersionInfo.SetVersion(2, 3, 2);
                            GMS2_3_2_NineSlice = reader.ReadPointerObjectUnique<NineSlice>();
                        }
                    }
                }

                switch (S_SpriteType)
                {
                    case SpriteType.Normal:
                        TextureItems.Deserialize(reader);
                        ParseMaskData(reader);
                        break;
                    case SpriteType.SWF:
                        {
                            if (reader.ReadInt32() != 8)
                                reader.Warnings.Add(new GMWarning("SWF format not correct"));
                            TextureItems.Deserialize(reader);

                            // Parse the actual data
                            reader.Pad(4);
                            int begin = reader.Offset;
                            int jpegTablesLength = (reader.ReadInt32() & ~int.MinValue);
                            if (reader.ReadInt32() != 8)
                                reader.Warnings.Add(new GMWarning("SWF format not correct"));
                            reader.Offset += jpegTablesLength;
                            reader.Pad(4);
                            reader.Offset += (reader.ReadInt32() * 8) + 4;
                            int frameCount = reader.ReadInt32();
                            reader.Offset += 16;
                            int maskCount = reader.ReadInt32();
                            reader.Offset += 8;
                            for (int i = 0; i < frameCount; i++)
                                reader.Offset += (reader.ReadInt32() * 100) + 16;
                            for (int i = 0; i < maskCount; i++)
                            {
                                reader.Offset += reader.ReadInt32();
                                reader.Pad(4);
                            }
                            int swfDataLength = reader.Offset - begin;
                            reader.Offset = begin;
                            S_Buffer = reader.ReadBytes(swfDataLength);
                        }
                        break;
                    case SpriteType.Spine:
                        {
                            reader.Pad(4);

                            int begin = reader.Offset;
                            reader.ReadUInt32(); // version number
                            int jsonLength = reader.ReadInt32();
                            int atlasLength = reader.ReadInt32();
                            int textureLength = reader.ReadInt32();
                            reader.ReadUInt32(); // atlas tex width
                            reader.ReadUInt32(); // atlas tex height
                            reader.Offset = begin;

                            S_Buffer = reader.ReadBytes(24 + jsonLength + atlasLength + textureLength);
                        }
                        break;
                }
            }
            else
            {
                // Normal, GM:S 1.4 sprite
                reader.Offset -= 4;
                TextureItems.Deserialize(reader);
                ParseMaskData(reader);
            }
        }

        private void ParseMaskData(GMDataReader reader)
        {
            int MaskCount = reader.ReadInt32();
            int len = ((Width + 7) / 8) * Height;

            CollisionMasks = new List<BufferRegion>();
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

        public override string ToString()
        {
            return $"Sprite: \"{Name.Content}\"";
        }

        public class SequenceReference : IGMSerializable
        {
            public GMSequence Sequence;

            public void Serialize(GMDataWriter writer)
            {
                writer.Write(1);
                Sequence.Serialize(writer);
            }

            public void Deserialize(GMDataReader reader)
            {
                if (reader.ReadInt32() != 1)
                    reader.Warnings.Add(new GMWarning("Unexpected version for sequence reference in sprite"));
                Sequence = new GMSequence();
                Sequence.Deserialize(reader);
            }

            public override string ToString()
            {
                return Sequence.ToString();
            }
        }

        public class NineSlice : IGMSerializable
        {
            public int Left, Top, Right, Bottom;
            public bool Enabled;
            public TileMode[] TileModes = new TileMode[5];

            public enum TileMode : int
            {
                Stretch = 0,
                Repeat = 1,
                Mirror = 2,
                BlankRepeat = 3,
                Hide = 4
            }

            public void Serialize(GMDataWriter writer)
            {
                writer.Write(Left);
                writer.Write(Top);
                writer.Write(Right);
                writer.Write(Bottom);
                writer.WriteWideBoolean(Enabled);
                for (int i = 0; i < 5; i++)
                    writer.Write((int)TileModes[i]);
            }

            public void Deserialize(GMDataReader reader)
            {
                Left = reader.ReadInt32();
                Top = reader.ReadInt32();
                Right = reader.ReadInt32();
                Bottom = reader.ReadInt32();
                Enabled = reader.ReadWideBoolean();
                for (int i = 0; i < 5; i++)
                    TileModes[i] = (TileMode)reader.ReadInt32();
            }
        }
    }
}
