using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Chunks
{
    public class GMChunkSPRT : GMChunk
    {
        public GMPointerList<GMSprite> List;

        public override void Serialize(GMDataWriter writer)
        {
            base.Serialize(writer);

            List.Serialize(writer, (writer, i, count) =>
            {
                while ((writer.Offset & 3) != 0)
                    writer.Offset++;
            }, (writer, i, count) => 
            { 
                if (i + 1 != count)
                {
                    while ((writer.Offset & 3) != 0)
                        writer.Offset++;
                }
            });
        }

        public override void Unserialize(GMDataReader reader)
        {
            base.Unserialize(reader);

            List = new GMPointerList<GMSprite>();
            List.Unserialize(reader);
        }
    }
}
