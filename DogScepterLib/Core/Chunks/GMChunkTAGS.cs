using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Chunks
{
    public class GMChunkTAGS : GMChunk
    {
        public List<GMString> AllTags;
        public GMUniquePointerList<AssetTags> AssetTagsList;

        public override void Serialize(GMDataWriter writer)
        {
            base.Serialize(writer);

            writer.Pad(4);
            writer.Write(1);

            writer.Write(AllTags.Count);
            foreach (GMString s in AllTags)
                writer.WritePointerString(s);

            AssetTagsList.Serialize(writer);
        }

        public override void Deserialize(GMDataReader reader)
        {
            base.Deserialize(reader);

            reader.Pad(4);

            int chunkVersion = reader.ReadInt32();
            if (chunkVersion != 1)
                reader.Warnings.Add(new GMWarning($"TAGS version is {chunkVersion}, expected 1"));

            int count = reader.ReadInt32();
            AllTags = new List<GMString>(count);
            for (int i = count; i > 0; i--)
                AllTags.Add(reader.ReadStringPointerObject());

            AssetTagsList = new GMUniquePointerList<AssetTags>();
            AssetTagsList.Deserialize(reader);
        }

        public class AssetTags : GMSerializable
        {
            public int ID;
            public List<GMString> Tags;

            public void Serialize(GMDataWriter writer)
            {
                writer.Write(ID);
                writer.Write(Tags.Count);
                foreach (GMString s in Tags)
                    writer.WritePointerString(s);
            }

            public void Deserialize(GMDataReader reader)
            {
                ID = reader.ReadInt32();
                int count = reader.ReadInt32();
                Tags = new List<GMString>(count);
                for (int i = count; i > 0; i--)
                    Tags.Add(reader.ReadStringPointerObject());
            }
        }
    }
}
