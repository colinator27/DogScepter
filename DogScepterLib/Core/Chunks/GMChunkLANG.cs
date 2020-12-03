using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Chunks
{
    public class GMChunkLANG : GMChunk
    {
        // Basic format for anyone interested:
        // There's a list of entries wtih string identifiers, and Language objects that contain the values for those entries

        public int Unknown1;
        public int LanguageCount;
        public int EntryCount;

        public List<GMString> EntryIDs = new List<GMString>();
        public List<Language> Languages = new List<Language>();

        public override void Serialize(GMDataWriter writer)
        {
            base.Serialize(writer);

            writer.Write(Unknown1);
            LanguageCount = Languages.Count;
            writer.Write(LanguageCount);
            EntryCount = EntryIDs.Count;
            writer.Write(EntryCount);

            foreach (GMString s in EntryIDs)
                writer.WritePointerString(s);

            foreach (Language l in Languages)
                l.Serialize(writer);
        }

        public override void Unserialize(GMDataReader reader)
        {
            base.Unserialize(reader);

            Unknown1 = reader.ReadInt32();
            LanguageCount = reader.ReadInt32();
            EntryCount = reader.ReadInt32();

            // Read the identifiers for each entry
            for (int i = 0; i < EntryCount; i++)
                EntryIDs.Add(reader.ReadStringPointerObject());

            // Read the data for each language
            for (int i = 0; i < LanguageCount; i++)
            {
                Language l = new Language();
                l.Unserialize(reader, EntryCount);
                Languages.Add(l);
            }
        }

        public class Language : GMSerializable
        {
            public GMString Name;
            public GMString Region;
            public List<GMString> Entries = new List<GMString>();
            // values that correspond to EntryIDs/EntryCount in main chunk

            public void Serialize(GMDataWriter writer)
            {
                writer.WritePointerString(Name);
                writer.WritePointerString(Region);
                foreach (GMString s in Entries)
                    writer.WritePointerString(s);
            }

            public void Unserialize(GMDataReader reader, int entryCount)
            {
                Name = reader.ReadStringPointerObject();
                Region = reader.ReadStringPointerObject();
                for (uint i = 0; i < entryCount; i++)
                    Entries.Add(reader.ReadStringPointerObject());
            }

            public void Unserialize(GMDataReader reader)
            {
                Unserialize(reader, 0);
            }
        }
    }
}
