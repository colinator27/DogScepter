using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Models
{

    public class GMBackground : GMSerializable
    {
        public GMString Name;
        public bool Transparent;
        public bool Smooth;
        public bool Preload;
        public GMTextureItem Texture;

        // GMS2 tiles
        public uint TileUnknown1; // Seems to always be 2
        public uint TileWidth;
        public uint TileHeight;
        public uint TileOutputBorderX; // A setting in the IDE, seems to only change the texture on compile,
        public uint TileOutputBorderY; // and not impact the runner(?)
        public uint TileColumns;
        public uint TileFrames; // amount of entries per tile
        public uint TileCount;
        public uint TileUnknown2; // Seems to always be 0
        public long TileFrameLength; // time for each frame in microseconds
        public List<List<uint>> TileIDs; // Contains entries per tile per frame

        public void Serialize(GMDataWriter writer)
        {
            writer.WritePointerString(Name);
            writer.WriteWideBoolean(Transparent);
            writer.WriteWideBoolean(Smooth);
            writer.WriteWideBoolean(Preload);
            writer.WritePointer(Texture);

            if (writer.VersionInfo.Major >= 2)
            {
                writer.Write(TileUnknown1);
                writer.Write(TileWidth);
                writer.Write(TileHeight);
                writer.Write(TileOutputBorderX);
                writer.Write(TileOutputBorderY);
                writer.Write(TileColumns);
                writer.Write(TileFrames);
                writer.Write(TileCount);
                writer.Write(TileUnknown2);
                writer.Write(TileFrameLength);

                foreach (List<uint> list in TileIDs)
                {
                    foreach (uint item in list)
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
            Texture = reader.ReadPointerObject<GMTextureItem>();

            if (reader.VersionInfo.Major >= 2)
            {
                TileUnknown1 = reader.ReadUInt32();
                TileWidth = reader.ReadUInt32();
                TileHeight = reader.ReadUInt32();
                TileOutputBorderX = reader.ReadUInt32();
                TileOutputBorderY = reader.ReadUInt32();
                TileColumns = reader.ReadUInt32();
                TileFrames = reader.ReadUInt32();
                TileCount = reader.ReadUInt32();
                TileUnknown2 = reader.ReadUInt32();
                TileFrameLength = reader.ReadInt64();

                TileIDs = new List<List<uint>>((int)TileCount);
                for (int i = 0; i < TileCount; i++)
                {
                    List<uint> tileFrames = new List<uint>((int)TileFrames);
                    TileIDs.Add(tileFrames);
                    for (int j = 0; j < TileFrames; j++)
                    {
                        tileFrames.Add(reader.ReadUInt32());
                    }
                }
            }

        }
    }
}
