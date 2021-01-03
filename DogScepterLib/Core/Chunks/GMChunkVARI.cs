using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Chunks
{
    class GMChunkVARI : GMChunk
    {
        public List<GMVariable> List;

        public uint InstanceVarCount;
        public uint MaxLocalVarCount;

        public override void Serialize(GMDataWriter writer)
        {
            base.Serialize(writer);

            if (writer.VersionInfo.FormatID > 14)
            {
                writer.Write(InstanceVarCount);
                writer.Write(InstanceVarCount);

                // Set MaxLocalVarCount to highest amount of locals within all entires
                MaxLocalVarCount = 0;
                foreach (GMLocalsEntry item in ((GMChunkFUNC)writer.Data.Chunks["FUNC"]).Locals)
                {
                    MaxLocalVarCount = (uint)Math.Max(MaxLocalVarCount, item.Entries.Count);
                }

                writer.Write(MaxLocalVarCount);
            }

            foreach (GMVariable variable in List)
            {
                variable.Serialize(writer);
            }
        }

        public override void Unserialize(GMDataReader reader)
        {
            base.Unserialize(reader);

            if (reader.VersionInfo.FormatID > 14)
            {
                InstanceVarCount = reader.ReadUInt32();
                reader.Offset += 4;
                MaxLocalVarCount = reader.ReadUInt32();
            }

            int varLength = (reader.VersionInfo.FormatID > 14) ? 20 : 12;

            List = new List<GMVariable>();
            while (reader.Offset + varLength <= StartOffset + Length)
            {
                GMVariable gVar = new GMVariable();
                gVar.Unserialize(reader);
                List.Add(gVar);
            }
        }
    }
}
