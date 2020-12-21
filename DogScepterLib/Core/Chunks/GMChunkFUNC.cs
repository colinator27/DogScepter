using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Chunks
{
    class GMChunkFUNC : GMChunk
    {
        public GMList<GMFunctionEntry> FunctionEntries;
        public GMList<GMLocalsEntry> Locals;

        public override void Serialize(GMDataWriter writer)
        {
            base.Serialize(writer);

            if (writer.VersionInfo.FormatID <= 14)
            {
                for (int i = 0; i < FunctionEntries.Count; i++)
                {
                    FunctionEntries[i].Serialize(writer);
                }
            }
            else
            {
                FunctionEntries.Serialize(writer);
                Locals.Serialize(writer);
            }
        }

        public override void Unserialize(GMDataReader reader)
        {
            base.Unserialize(reader);

            FunctionEntries = new GMList<GMFunctionEntry>();
            Locals = new GMList<GMLocalsEntry>();

            if (reader.VersionInfo.FormatID <= 14)
            {
                int startOff = reader.Offset;
                while (reader.Offset + 12 <= startOff + Length)
                {
                    GMFunctionEntry entry = new GMFunctionEntry();
                    entry.Unserialize(reader);
                    FunctionEntries.Add(entry);
                }
            }
            else
            {
                FunctionEntries.Unserialize(reader);
                Locals.Unserialize(reader);
            }
        }
    }
}
