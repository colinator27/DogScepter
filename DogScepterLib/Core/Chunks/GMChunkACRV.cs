using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Chunks
{
    public class GMChunkACRV : GMChunk
    {
        public GMPointerList<GMAnimCurve> List;
        public override void Serialize(GMDataWriter writer)
        {
            base.Serialize(writer);

            List.Serialize(writer);
        }

        public override void Unserialize(GMDataReader reader)
        {
            base.Unserialize(reader);

            int animCurveVer = reader.ReadInt32();
            if (animCurveVer != 1)
                reader.Warnings.Add(new GMWarning(string.Format("Animation Curve version is {0}, expected 1", animCurveVer)));

            List = new GMPointerList<GMAnimCurve>();
            List.Unserialize(reader);
        }
    }
}
