using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core
{
    /// <summary>
    /// A GameMaker resource that can be read and written.
    /// </summary>
    public interface GMSerializable
    {
        void Serialize(GMDataWriter writer);
        void Unserialize(GMDataReader reader);
    }
}
