using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core
{
    public interface GMSerializable
    {
        void Serialize(GMDataWriter writer);
        void Unserialize(GMDataReader reader);
    }
}
