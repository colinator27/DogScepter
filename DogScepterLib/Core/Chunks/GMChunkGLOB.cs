using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Chunks
{
    // Global init code entries
    public class GMChunkGLOB : GMChunk
    {
        public List<int> List;

        public override void Serialize(GMDataWriter writer)
        {
            base.Serialize(writer);

            writer.Write(List.Count);
            foreach (int item in List)
                writer.Write(item);
        }

        public override void Deserialize(GMDataReader reader)
        {
            base.Deserialize(reader);

            int count = reader.ReadInt32();
            List = new List<int>(count);
            for (int i = count; i > 0; i--)
                List.Add(reader.ReadInt32());
        }
    }
}
