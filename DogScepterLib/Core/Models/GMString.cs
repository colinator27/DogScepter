using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace DogScepterLib.Core.Models
{

    /// <summary>
    /// An UTF-8 string, usually contained within the STRG chunk.
    /// </summary>
    [DebuggerDisplay("{Content}")]
    public class GMString : IGMSerializable
    {
        public string Content;

        public void Serialize(GMDataWriter writer)
        {
            writer.WriteGMString(Content);
        }

        public void Deserialize(GMDataReader reader)
        {
            Content = reader.ReadGMString();
        }

        public override string ToString()
        {
            return Content;
        }
    }
}
