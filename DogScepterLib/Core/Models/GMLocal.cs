using DogScepterLib.Core.Chunks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace DogScepterLib.Core.Models
{
    /// <summary>
    /// Contains a list of local variables, per script entry (Assigned to scripts by having the same Name property).
    /// </summary>
    public class GMLocalsEntry : IGMSerializable
    {
        public GMString Name;
        public List<GMLocal> Entries;

        public GMLocalsEntry()
        {
            // Default constructor
        }

        public GMLocalsEntry(GMString name)
        {
            Name = name;
            Entries = new();
        }

        public void Serialize(GMDataWriter writer)
        {
            writer.Write(Entries.Count);
            writer.WritePointerString(Name);
            for (int i = 0; i < Entries.Count; i++)
            {
                Entries[i].Serialize(writer);
            }
        }

        public void Deserialize(GMDataReader reader)
        {
            Entries = new List<GMLocal>();

            int count = reader.ReadInt32();
            Name = reader.ReadStringPointerObject();

            for (int i = 0; i < count; i++)
            {
                GMLocal local = new GMLocal();
                local.Deserialize(reader);
                Entries.Add(local);
            }
        }

        /// <summary>
        /// Adds a new local to this code local entry.
        /// Updates relevant related information in other locations.
        /// </summary>
        public void AddLocal(GMData data, string name, GMCode code)
        {
            Entries.Add(new GMLocal(data, Entries, name));
            var vari = data.GetChunk<GMChunkVARI>();
            if (vari.MaxLocalVarCount < Entries.Count)
                vari.MaxLocalVarCount = Entries.Count;
            code.LocalsCount = (short)Entries.Count;
        }

        /// <summary>
        /// Clears all locals from this code local entry.
        /// Updates relevant related information in other locations.
        /// </summary>
        public void ClearLocals(GMCode code)
        {
            Entries.Clear();
            code.LocalsCount = 0;
        }

        public override string ToString()
        {
            return $"Locals for \"{Name.Content}\"";
        }
    }

    [DebuggerDisplay("{Name.Content}")]
    public class GMLocal : IGMSerializable
    {
        public int Index;
        public GMString Name;

        public GMLocal()
        {
            // Default constructor
        }

        public GMLocal(GMData data, IList<GMLocal> list, string name)
        {
            if (data.VersionInfo.IsVersionAtLeast(2, 3))
                Name = data.DefineString(name, out Index);
            else
            {
                Name = data.DefineString(name);
                Index = list.Count;
            }
        }    

        public void Serialize(GMDataWriter writer)
        {
            writer.Write(Index);
            writer.WritePointerString(Name);
        }

        public void Deserialize(GMDataReader reader)
        {
            Index = reader.ReadInt32();
            Name = reader.ReadStringPointerObject();
        }

        public override string ToString()
        {
            return Name.Content;
        }


    }
}
