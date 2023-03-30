using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Models
{
    /// <summary>
    /// Contains metadata about GameMaker texture groups.
    /// </summary>
    public class GMTextureGroupInfo : IGMSerializable
    {
        public GMString Name;
        public GMList<ResourceID> TexturePageIDs = new GMList<ResourceID>();
        public GMList<ResourceID> SpriteIDs = new GMList<ResourceID>();
        public GMList<ResourceID> SpineSpriteIDs = new GMList<ResourceID>();
        public GMList<ResourceID> FontIDs = new GMList<ResourceID>();
        public GMList<ResourceID> TilesetIDs = new GMList<ResourceID>();

        // 2022.9+ fields
        public GMString Directory;
        public GMString Extension;
        public TextureGroupLoadType LoadType;

        public enum TextureGroupLoadType
        {
            InFile = 0,
            SeparateGroup = 1,
            SeparateTextures = 2
        }

        public void Serialize(GMDataWriter writer)
        {
            writer.WritePointerString(Name);

            if (writer.VersionInfo.IsVersionAtLeast(2022, 9))
            {
                writer.WritePointerString(Directory);
                writer.WritePointerString(Extension);
                writer.Write((int)LoadType);
            }

            writer.WritePointer(TexturePageIDs);
            writer.WritePointer(SpriteIDs);
            if (!writer.VersionInfo.IsVersionAtLeast(2023, 1))
                writer.WritePointer(SpineSpriteIDs);
            writer.WritePointer(FontIDs);
            writer.WritePointer(TilesetIDs);

            writer.WriteObjectPointer(TexturePageIDs);
            TexturePageIDs.Serialize(writer);

            writer.WriteObjectPointer(SpriteIDs);
            SpriteIDs.Serialize(writer);
            if (!writer.VersionInfo.IsVersionAtLeast(2023, 1))
            {
                writer.WriteObjectPointer(SpineSpriteIDs);
                SpineSpriteIDs.Serialize(writer);
            }
            writer.WriteObjectPointer(FontIDs);
            FontIDs.Serialize(writer);
            writer.WriteObjectPointer(TilesetIDs);
            TilesetIDs.Serialize(writer);
        }

        public void Deserialize(GMDataReader reader)
        {
            Name = reader.ReadStringPointerObject();
            if (reader.VersionInfo.IsVersionAtLeast(2022, 9))
            {
                Directory = reader.ReadStringPointerObject();
                Extension = reader.ReadStringPointerObject();
                LoadType = (TextureGroupLoadType)reader.ReadInt32();
            }
            TexturePageIDs = reader.ReadPointerObjectUnique<GMList<ResourceID>>();
            SpriteIDs = reader.ReadPointerObjectUnique<GMList<ResourceID>>();
            if (!reader.VersionInfo.IsVersionAtLeast(2023, 1))
                SpineSpriteIDs = reader.ReadPointerObjectUnique<GMList<ResourceID>>();
            FontIDs = reader.ReadPointerObjectUnique<GMList<ResourceID>>();
            TilesetIDs = reader.ReadPointerObjectUnique<GMList<ResourceID>>();
        }

        public override string ToString()
        {
            return $"Texture Group Info: \"{Name.Content}\"";
        }

        public class ResourceID : IGMSerializable
        {
            public int ID;

            public void Serialize(GMDataWriter writer)
            {
                writer.Write(ID);
            }

            public void Deserialize(GMDataReader reader)
            {
                ID = reader.ReadInt32();
            }
        }
    }
}
