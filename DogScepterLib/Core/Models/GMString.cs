using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Models
{
    public class GMString : GMSerializable
    {
        public string Content;

        public void Serialize(GMDataWriter writer)
        {
            writer.WriteGMString(Content);
        }

        public void Unserialize(GMDataReader reader)
        {
            Content = reader.ReadGMString();
        }
    }
}
