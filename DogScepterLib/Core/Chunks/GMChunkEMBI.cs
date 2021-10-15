using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Chunks
{
    public class GMChunkEMBI : GMChunk
    {
        public GMList<EmbeddedImage> List;

        public override void Serialize(GMDataWriter writer)
        {
            base.Serialize(writer);

            writer.Write(1);

            List.Serialize(writer);
        }

        public override void Unserialize(GMDataReader reader)
        {
            base.Unserialize(reader);

            int chunkVersion = reader.ReadInt32();
            if (chunkVersion != 1)
                reader.Warnings.Add(new GMWarning($"EMBI version is {chunkVersion}, expected 1"));

            List = new GMList<EmbeddedImage>();
            List.Unserialize(reader);
        }

        public class EmbeddedImage : GMSerializable
        {
            public GMString Name;
            public GMTextureItem TextureItem;

            public void Serialize(GMDataWriter writer)
            {
                writer.WritePointerString(Name);
                writer.WritePointer(TextureItem);
            }

            public void Unserialize(GMDataReader reader)
            {
                Name = reader.ReadStringPointerObject();
                TextureItem = reader.ReadPointer<GMTextureItem>();
            }
        }
    }
}
