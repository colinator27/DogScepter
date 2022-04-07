using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Models
{
    /// <summary>
    /// Contains binary audio data.
    /// </summary>
    public class GMAudio : IGMSerializable
    {
        public BufferRegion Data;

        public void Serialize(GMDataWriter writer)
        {
            writer.Write(Data.Length);
            writer.Write(Data);
        }

        public void Deserialize(GMDataReader reader)
        {
            int length = reader.ReadInt32();
            Data = reader.ReadBytes(length);
        }
    }
}
