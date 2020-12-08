using System;
using System.Collections.Generic;
using System.Text;
using SkiaSharp;

namespace DogScepterLib.Core.Models
{
    public class GMPath : GMSerializable
    {
        public GMString Name;
        public bool Smooth;
        public bool Closed;
        public uint Precision;
        public GMList<GMPathPoint> Points;

        public void Serialize(GMDataWriter writer)
        {
            writer.WritePointerString(Name);
            writer.WriteWideBoolean(Smooth);
            writer.WriteWideBoolean(Closed);
            writer.Write(Precision);
            Points.Serialize(writer);
        }

        public void Unserialize(GMDataReader reader)
        {
            Name = reader.ReadStringPointerObject();
            Smooth = reader.ReadWideBoolean();
            Closed = reader.ReadWideBoolean();
            Precision = reader.ReadUInt32();
            Points = new GMList<GMPathPoint>();
            Points.Unserialize(reader);
        }
    }

    public class GMPathPoint : GMSerializable
    {
        public float X;
        public float Y;
        public float Speed;

        public void Serialize(GMDataWriter writer)
        {
            writer.Write(X);
            writer.Write(Y);
            writer.Write(Speed);
        }

        public void Unserialize(GMDataReader reader)
        {
            X = reader.ReadSingle();
            Y = reader.ReadSingle();
            Speed = reader.ReadSingle();
        }
    }
}
