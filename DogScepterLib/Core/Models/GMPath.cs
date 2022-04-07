using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Models
{
    /// <summary>
    /// Contains a GameMaker path.
    /// </summary>
    public class GMPath : GMNamedSerializable
    {
        public GMString Name { get; set; }
        public bool Smooth;
        public bool Closed;
        public uint Precision;
        public GMList<Point> Points;

        public void Serialize(GMDataWriter writer)
        {
            writer.WritePointerString(Name);
            writer.WriteWideBoolean(Smooth);
            writer.WriteWideBoolean(Closed);
            writer.Write(Precision);
            Points.Serialize(writer);
        }

        public void Deserialize(GMDataReader reader)
        {
            Name = reader.ReadStringPointerObject();
            Smooth = reader.ReadWideBoolean();
            Closed = reader.ReadWideBoolean();
            Precision = reader.ReadUInt32();
            Points = new GMList<Point>();
            Points.Deserialize(reader);
        }

        public override string ToString()
        {
            return $"Path: \"{Name.Content}\"";
        }

        public class Point : GMSerializable
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

            public void Deserialize(GMDataReader reader)
            {
                X = reader.ReadSingle();
                Y = reader.ReadSingle();
                Speed = reader.ReadSingle();
            }
        }
    }
}
