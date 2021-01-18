using System;
using System.Collections.Generic;
using System.Text;
using DogScepterLib.Core.Models;

namespace DogScepterLib.Core.Chunks
{
    public class GMChunkOBJT : GMChunk
    {
        public GMPointerList<GMObject> List;

        public override void Serialize(GMDataWriter writer)
        {
            base.Serialize(writer);

            List.Serialize(writer);
        }

        public override void Unserialize(GMDataReader reader)
        {
            base.Unserialize(reader);

            List = new GMPointerList<GMObject>();
            List.Unserialize(reader);
        }
    }
}
