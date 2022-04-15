using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Chunks
{
    public class GMChunkFUNC : GMChunk
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

        public override void Deserialize(GMDataReader reader)
        {
            base.Deserialize(reader);

            FunctionEntries = new GMList<GMFunctionEntry>();
            Locals = new GMList<GMLocalsEntry>();

            if (reader.VersionInfo.FormatID <= 14)
            {
                int startOff = reader.Offset;
                while (reader.Offset + 12 <= startOff + Length)
                {
                    GMFunctionEntry entry = new GMFunctionEntry();
                    entry.Deserialize(reader);
                    FunctionEntries.Add(entry);
                }
            }
            else
            {
                FunctionEntries.Deserialize(reader);
                Locals.Deserialize(reader);
            }
        }

        public GMFunctionEntry FindOrDefine(string name, GMData data)
        {
            // Search for an existing function entry
            // todo? might want to cache this with a map?
            foreach (var func in FunctionEntries)
            {
                if (func.Name.Content == name)
                    return func;
            }

            // Create a new function, add to list, and return it
            GMFunctionEntry res = new()
            {
                Name = data.DefineString(name)
            };
            FunctionEntries.Add(res);
            return res;
        }

        public GMLocalsEntry FindLocalsEntry(string name)
        {
            // todo? might want to cache this with a map?
            foreach (var entry in Locals)
            {
                if (entry.Name.Content == name)
                    return entry;
            }
            return null;
        }
    }
}
