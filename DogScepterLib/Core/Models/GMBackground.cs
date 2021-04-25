using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Models
{
    /// <summary>
    /// Contains a tileset since GameMaker Studio 2, contains a background otherwise.
    /// </summary>
    public class GMBackground : GMSerializable
    {
        public GMString Name;
        public bool Transparent;
        public bool Smooth;
        public bool Preload;
        public GMTextureItem TextureItem;

        // GMS2 tiles
        public uint TileUnknown1 = 2; // Seems to always be 2
        public uint TileWidth;
        public uint TileHeight;
        public uint TileOutputBorderX; // A setting in the IDE, seems to only change the texture on compile,
        public uint TileOutputBorderY; // and not impact the runner(?)
        public uint TileColumns;
        public uint TileUnknown2 = 0; // Seems to always be 0
        public long TileFrameLength; // time for each frame in microseconds
        public List<List<uint>> Tiles; // Contains entries per tile per frame

        public void Serialize(GMDataWriter writer)
        {
            writer.WritePointerString(Name);
            writer.WriteWideBoolean(Transparent);
            writer.WriteWideBoolean(Smooth);
            writer.WriteWideBoolean(Preload);
            writer.WritePointer(TextureItem);

            if (writer.VersionInfo.Major >= 2)
            {
                writer.Write(TileUnknown1);
                writer.Write(TileWidth);
                writer.Write(TileHeight);
                writer.Write(TileOutputBorderX);
                writer.Write(TileOutputBorderY);
                writer.Write(TileColumns);
                writer.Write((uint)Tiles[0].Count);
                writer.Write((uint)Tiles.Count);
                writer.Write(TileUnknown2);
                writer.Write(TileFrameLength);

                for (int i = 0; i < Tiles.Count; i++)
                {
                    if (i != 0 && Tiles[i].Count != Tiles[i-1].Count)
                        writer.Warnings.Add(new GMWarning("Amount of frames is different across tiles", GMWarning.WarningLevel.Severe));
                    foreach (uint item in Tiles[i])
                    {
                        writer.Write(item);
                    }
                }
            }
        }

        public void Unserialize(GMDataReader reader)
        {
            Name = reader.ReadStringPointerObject();
            Transparent = reader.ReadWideBoolean();
            Smooth = reader.ReadWideBoolean();
            Preload = reader.ReadWideBoolean();
            TextureItem = reader.ReadPointerObject<GMTextureItem>();

            if (reader.VersionInfo.Major >= 2)
            {
                TileUnknown1 = reader.ReadUInt32();
                if (TileUnknown1 != 2)
                    reader.Warnings.Add(new GMWarning("Expected 2 in BGND"));
                TileWidth = reader.ReadUInt32();
                TileHeight = reader.ReadUInt32();
                TileOutputBorderX = reader.ReadUInt32();
                TileOutputBorderY = reader.ReadUInt32();
                TileColumns = reader.ReadUInt32();
                uint tileFrameCount = reader.ReadUInt32();
                uint tileCount = reader.ReadUInt32();
                TileUnknown2 = reader.ReadUInt32();
                if (TileUnknown2 != 0)
                    reader.Warnings.Add(new GMWarning("Expected 0 in BGND"));
                TileFrameLength = reader.ReadInt64();

                Tiles = new List<List<uint>>((int)tileCount);
                for (int i = 0; i < tileCount; i++)
                {
                    List<uint> tileFrames = new List<uint>((int)tileFrameCount);
                    Tiles.Add(tileFrames);
                    for (int j = 0; j < tileFrameCount; j++)
                    {
                        tileFrames.Add(reader.ReadUInt32());
                    }
                }
            }

        }

        public override string ToString()
        {
            return $"Background: \"{Name.Content}\"";
        }
    }
}
