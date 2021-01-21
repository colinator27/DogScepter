using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace DogScepterLib.Core.Models
{

    /// <summary>
    /// A UTF-8 string, usually contained within the STRG chunk.
    /// </summary>
    [DebuggerDisplay("{Content}")]
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

        public override string ToString()
        {
            return Content;
        }
    }
}
