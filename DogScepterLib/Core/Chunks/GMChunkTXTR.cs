using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Chunks
{
    public class GMChunkTXTR : GMChunk
    {
        public GMPointerList<GMTexturePage> List;

        public override void Serialize(GMDataWriter writer)
        {
            base.Serialize(writer);
        }

        public override void Unserialize(GMDataReader reader)
        {
            base.Unserialize(reader);

            List = new GMPointerList<GMTexturePage>();
            List.Unserialize(reader);
        }
    }
}
