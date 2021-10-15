using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Models
{
    /// <summary>
    /// Contains metadata about GameMaker texture groups.
    /// </summary>
    public class GMTextureGroupInfo : GMSerializable
    {
        public GMString Name;
        public GMList<ResourceID> TexturePageIDs = new GMList<ResourceID>();
        public GMList<ResourceID> SpriteIDs = new GMList<ResourceID>();
        public GMList<ResourceID> SpineSpriteIDs = new GMList<ResourceID>();
        public GMList<ResourceID> FontIDs = new GMList<ResourceID>();
        public GMList<ResourceID> TilesetIDs = new GMList<ResourceID>();

        public void Serialize(GMDataWriter writer)
        {
            writer.WritePointerString(Name);

            writer.WritePointer(TexturePageIDs);
            writer.WritePointer(SpriteIDs);
            writer.WritePointer(SpineSpriteIDs);
            writer.WritePointer(FontIDs);
            writer.WritePointer(TilesetIDs);

            writer.WriteObjectPointer(TexturePageIDs);
            TexturePageIDs.Serialize(writer);
            writer.WriteObjectPointer(SpriteIDs);
            SpriteIDs.Serialize(writer);
            writer.WriteObjectPointer(SpineSpriteIDs);
            SpineSpriteIDs.Serialize(writer);
            writer.WriteObjectPointer(FontIDs);
            FontIDs.Serialize(writer);
            writer.WriteObjectPointer(TilesetIDs);
            TilesetIDs.Serialize(writer);
        }

        public void Unserialize(GMDataReader reader)
        {
            Name = reader.ReadStringPointerObject();
            TexturePageIDs = reader.ReadPointerObjectUnique<GMList<ResourceID>>();
            SpriteIDs = reader.ReadPointerObjectUnique<GMList<ResourceID>>();
            SpineSpriteIDs = reader.ReadPointerObjectUnique<GMList<ResourceID>>();
            FontIDs = reader.ReadPointerObjectUnique<GMList<ResourceID>>();
            TilesetIDs = reader.ReadPointerObjectUnique<GMList<ResourceID>>();
        }

        public override string ToString()
        {
            return $"Texture Group Info: \"{Name.Content}\"";
        }

        public class ResourceID : GMSerializable
        {
            public int ID;

            public void Serialize(GMDataWriter writer)
            {
                writer.Write(ID);
            }

            public void Unserialize(GMDataReader reader)
            {
                ID = reader.ReadInt32();
            }
        }
    }
}
