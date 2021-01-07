using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Chunks
{
    public class GMChunkGLOB : GMChunk
    {
        public List<int> List;

        public override void Serialize(GMDataWriter writer)
        {
            base.Serialize(writer);

            writer.Write(List.Count);
            foreach (int item in List)
            {
                writer.Write(item);
            }
        }

        public override void Unserialize(GMDataReader reader)
        {
            base.Unserialize(reader);

            List = new List<int>();
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                List.Add(reader.ReadInt32());
            }
        }
    }
}
