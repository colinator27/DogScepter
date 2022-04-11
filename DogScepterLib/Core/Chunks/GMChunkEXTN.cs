using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Chunks
{
    public class GMChunkEXTN : GMChunk
    {
        public GMUniquePointerList<GMExtension> List;

        public override void Serialize(GMDataWriter writer)
        {
            base.Serialize(writer);

            List.Serialize(writer);

            foreach (GMExtension e in List)
            {
                if (e.ProductID != null)
                    writer.Write(e.ProductID?.ToByteArray());
            }
        }

        public override void Deserialize(GMDataReader reader)
        {
            base.Deserialize(reader);

            List = new GMUniquePointerList<GMExtension>();
            List.Deserialize(reader);

            // Product ID information for each extension
            if (reader.VersionInfo.IsVersionAtLeast(1, 0, 0, 9999))
            {
                for (int i = 0; i < List.Count; i++)
                    List[i].ProductID = new Guid(reader.ReadBytes(16).Memory.ToArray());
            }
        }
    }
}
