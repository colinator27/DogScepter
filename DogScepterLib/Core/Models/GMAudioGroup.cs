using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Models
{
    public class GMAudioGroup : GMSerializable
    {
        public GMString Name;

        public void Serialize(GMDataWriter writer)
        {
            writer.WritePointerString(Name);
        }

        public void Unserialize(GMDataReader reader)
        {
            Name = reader.ReadStringPointerObject();
        }
    }
}
