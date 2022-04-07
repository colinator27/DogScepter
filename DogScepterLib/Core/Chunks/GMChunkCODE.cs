using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Chunks
{
    public class GMChunkCODE : GMChunk
    {
        public GMUniquePointerList<GMCode> List;

        public override void Serialize(GMDataWriter writer)
        {
            base.Serialize(writer);

            if (List != null)
            {
                if (writer.VersionInfo.FormatID <= 14)
                    List.Serialize(writer);
                else
                {
                    List.Serialize(writer, (GMDataWriter writer, int index, int count) =>
                    {
                        if (index == 0)
                        {
                            // Serialize bytecode before entries
                            foreach (GMCode c in List)
                            {
                                if (!writer.PointerOffsets.ContainsKey(c.BytecodeEntry))
                                {
                                    writer.WriteObjectPointer(c.BytecodeEntry);
                                    c.BytecodeEntry.Serialize(writer);
                                }
                            }
                        }
                    });
                }
            }
        }

        public override void Deserialize(GMDataReader reader)
        {
            base.Deserialize(reader);

            if (Length == 0)
                return; // In YYC, before bytecode 17, CODE is empty

            List = new GMUniquePointerList<GMCode>();
            List.Deserialize(reader);
        }
    }
}
