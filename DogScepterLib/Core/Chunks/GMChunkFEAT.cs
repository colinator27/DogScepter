using DogScepterLib.Core.Models;
using System.Collections.Generic;

namespace DogScepterLib.Core.Chunks
{
    public class GMChunkFEAT : GMChunk
    {
        public List<GMString> FeatureFlags;

        public override void Serialize(GMDataWriter writer)
        {
            base.Serialize(writer);

            writer.Pad(4);

            writer.Write(FeatureFlags.Count);
            foreach (GMString s in FeatureFlags)
                writer.WritePointerString(s);
        }

        public override void Deserialize(GMDataReader reader)
        {
            base.Deserialize(reader);

            reader.Pad(4);

            int count = reader.ReadInt32();
            FeatureFlags = new List<GMString>(count);
            for (int i = count; i > 0; i--)
                FeatureFlags.Add(reader.ReadStringPointerObject());
        }
    }
}
