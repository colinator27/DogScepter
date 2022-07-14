using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Models
{
    /// <summary>
    /// Contains a GameMaker extension.
    /// </summary>
    public class GMExtension : IGMSerializable
    {
        public GMString EmptyString;
        public GMString Name;
        public GMString ClassName;
        public GMPointerList<ExtensionFile> Files;
        public GMPointerList<ExtensionOption> Options;

        public Guid? ProductID = null; // Set seemingly in 1.4.9999 and up

        public enum ExtensionKind : uint
        {
            Unknown0 = 0,
            DLL = 1,
            GML = 2,
            Unknown3 = 3,
            Generic = 4,
            JS = 5
        }
        public enum ExtensionValueType : uint
        {
            String = 1,
            Double = 2
        }

        public void Serialize(GMDataWriter writer)
        {
            writer.WritePointerString(EmptyString);
            writer.WritePointerString(Name);
            writer.WritePointerString(ClassName);

            if (writer.VersionInfo.IsVersionAtLeast(2022, 6))
            {
                writer.WritePointer(Files);
                writer.WritePointer(Options);
                writer.WriteObjectPointer(Files);
                Files.Serialize(writer);
                writer.WriteObjectPointer(Options);
                Options.Serialize(writer);
            }
            else
            {
                Files.Serialize(writer);
            }
        }

        public void Deserialize(GMDataReader reader)
        {
            EmptyString = reader.ReadStringPointerObject();
            Name = reader.ReadStringPointerObject();
            ClassName = reader.ReadStringPointerObject();

            if (reader.VersionInfo.IsVersionAtLeast(2022, 6))
            {
                Files = reader.ReadPointerObjectUnique<GMPointerList<ExtensionFile>>();
                Options = reader.ReadPointerObjectUnique<GMPointerList<ExtensionOption>>();
            }
            else
            {
                Files = new GMPointerList<ExtensionFile>();
                Files.Deserialize(reader);
            }
        }

        public override string ToString()
        {
            return $"Extension: \"{Name.Content}\"";
        }

        public class ExtensionFile : IGMSerializable
        {
            public GMString Filename;
            public GMString FinalFunction;
            public GMString InitFunction;
            public ExtensionKind Kind;
            public GMPointerList<ExtensionFunction> Functions;

            public void Serialize(GMDataWriter writer)
            {
                writer.WritePointerString(Filename);
                writer.WritePointerString(FinalFunction);
                writer.WritePointerString(InitFunction);
                writer.Write((uint)Kind);
                Functions.Serialize(writer);
            }

            public void Deserialize(GMDataReader reader)
            {
                Filename = reader.ReadStringPointerObject();
                FinalFunction = reader.ReadStringPointerObject();
                InitFunction = reader.ReadStringPointerObject();
                Kind = (ExtensionKind)reader.ReadUInt32();
                Functions = new GMPointerList<ExtensionFunction>();
                Functions.Deserialize(reader);
            }

            public override string ToString()
            {
                return $"Extension File: \"{Filename.Content}\"";
            }
        }

        public class ExtensionFunction : IGMSerializable
        {
            public GMString Name;
            public int ID;
            public int Kind;
            public ExtensionValueType ReturnType;
            public GMString ExternalName;
            public List<ExtensionValueType> ArgumentTypes;

            public void Serialize(GMDataWriter writer)
            {
                writer.WritePointerString(Name);
                writer.Write(ID);
                writer.Write(Kind);
                writer.Write((uint)ReturnType);
                writer.WritePointerString(ExternalName);

                writer.Write(ArgumentTypes.Count);
                for (int i = 0; i < ArgumentTypes.Count; i++)
                    writer.Write((uint)ArgumentTypes[i]);
            }

            public void Deserialize(GMDataReader reader)
            {
                Name = reader.ReadStringPointerObject();
                ID = reader.ReadInt32();
                Kind = reader.ReadInt32();
                ReturnType = (ExtensionValueType)reader.ReadUInt32();
                ExternalName = reader.ReadStringPointerObject();

                ArgumentTypes = new List<ExtensionValueType>();
                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                    ArgumentTypes.Add((ExtensionValueType)reader.ReadUInt32());
            }

            public override string ToString()
            {
                return $"Extension Function: \"{Name.Content}\"";
            }
        }

        public class ExtensionOption : IGMSerializable
        {
            public enum OptionKind : int
            {
                Boolean = 0,
                Number = 1,
                String = 2
            }

            public GMString Name;
            public GMString Value;
            public OptionKind Kind;

            public void Serialize(GMDataWriter writer)
            {
                writer.WritePointerString(Name);
                writer.WritePointerString(Value);
                writer.Write((int)Kind);
            }

            public void Deserialize(GMDataReader reader)
            {
                Name = reader.ReadStringPointerObject();
                Value = reader.ReadStringPointerObject();
                Kind = (OptionKind)reader.ReadInt32();
            }

            public override string ToString()
            {
                return $"Extension Option: \"{Name.Content}\"";
            }
        }
    }
}
