using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Chunks
{
    public class GMChunkSHDR : GMChunk
    {
        public List<GMShader> List;

        public override void Serialize(GMDataWriter writer)
        {
            base.Serialize(writer);

            writer.Write(List.Count);
            foreach (GMShader s in List)
                writer.WritePointer(s);
            foreach (GMShader s in List)
            {
                writer.WriteObjectPointer(s);
                s.Serialize(writer);
            }
        }

        public override void Deserialize(GMDataReader reader)
        {
            base.Deserialize(reader);

            reader.Offset -= 4;
            int chunkEnd = reader.Offset + 4 + reader.ReadInt32();

            int count = reader.ReadInt32();
            int[] ptrs = new int[count];
            for (int i = 0; i < count; i++)
                ptrs[i] = reader.ReadInt32();
            List = new List<GMShader>();
            for (int i = 0; i < count; i++)
            {
                GMShader s = new GMShader();
                reader.Offset = ptrs[i];
                if (i < count - 1)
                    s.Deserialize(reader, ptrs[i + 1]);
                else
                    s.Deserialize(reader, chunkEnd);
                List.Add(s);
            }
        }
    }
}
