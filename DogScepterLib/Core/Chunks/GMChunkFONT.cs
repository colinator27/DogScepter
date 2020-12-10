using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Chunks
{
    public class GMChunkFONT : GMChunk
    {
        public GMPointerList<GMFont> List = new GMPointerList<GMFont>();

        public override void Serialize(GMDataWriter writer)
        {
            base.Serialize(writer);

            List.Serialize(writer);

            // Whatever this is
            for (short i = 0; i < 0x80; i++)
                writer.Write(i);
            for (short i = 0; i < 0x80; i++)
                writer.Write((short)0x3f);
        }

        public override void Unserialize(GMDataReader reader)
        {
            base.Unserialize(reader);

            List.Unserialize(reader);

            // Whatever this is
            for (short i = 0; i < 0x80; i++)
                if (reader.ReadInt16() != i)
                    reader.Warnings.Add(new GMWarning("Incorrect weird values in FONT"));
            for (short i = 0; i < 0x80; i++)
                if (reader.ReadInt16() != 0x3f)
                    reader.Warnings.Add(new GMWarning("Incorrect weird values in FONT (part 2)"));
        }
    }
}
