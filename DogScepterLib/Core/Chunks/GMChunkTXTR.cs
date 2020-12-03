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

            List.Serialize(writer);
            foreach (GMTexturePage tpe in List)
                tpe.TextureData.Serialize(writer);

            writer.Pad(4);
        }

        public override void Unserialize(GMDataReader reader)
        {
            base.Unserialize(reader);

            List = new GMPointerList<GMTexturePage>();
            List.Unserialize(reader);
        }
    }
}
