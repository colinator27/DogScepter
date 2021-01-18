using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Chunks
{
    public class GMChunkTAGS : GMChunk
    {
        public List<GMString> AllTags;
        public GMPointerList<AssetTags> AssetTagsList;

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

        public override void Unserialize(GMDataReader reader)
        {
            base.Unserialize(reader);

            reader.Pad(4);

            int chunkVersion = reader.ReadInt32();
            if (chunkVersion != 1)
                reader.Warnings.Add(new GMWarning(string.Format("TAGS version is {0}, expected 1", chunkVersion)));

            AllTags = new List<GMString>();
            for (int i = reader.ReadInt32(); i > 0; i--)
                AllTags.Add(reader.ReadStringPointerObject());

            AssetTagsList = new GMPointerList<AssetTags>();
            AssetTagsList.Unserialize(reader);
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

            public void Unserialize(GMDataReader reader)
            {
                ID = reader.ReadInt32();
                Tags = new List<GMString>();
                for (int i = reader.ReadInt32(); i > 0; i--)
                    Tags.Add(reader.ReadStringPointerObject());
            }
        }
    }
}
