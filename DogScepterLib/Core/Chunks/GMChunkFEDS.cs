using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Chunks
{
    public class GMChunkFEDS : GMChunk
    {
        public List<GMString> FilterEffects;

        public override void Serialize(GMDataWriter writer)
        {
            base.Serialize(writer);

            writer.Pad(4);
            writer.Write(1);

            writer.Write(FilterEffects.Count);
            foreach (GMString s in FilterEffects)
                writer.WritePointerString(s);
        }

        public override void Unserialize(GMDataReader reader)
        {
            base.Unserialize(reader);

            reader.Pad(4);

            int chunkVersion = reader.ReadInt32();
            if (chunkVersion != 1)
                reader.Warnings.Add(new GMWarning($"FEDS version is {chunkVersion}, expected 1"));

            int count = reader.ReadInt32();
            FilterEffects = new List<GMString>(count);
            for (int i = count; i > 0; i--)
                FilterEffects.Add(reader.ReadStringPointerObject());
        }
    }
}
