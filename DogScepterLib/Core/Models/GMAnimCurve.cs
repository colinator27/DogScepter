using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Models
{
    public class GMAnimCurve : GMSerializable
    {
        public enum GraphTypeEnum
        {
            unknown1 = 0,
            unknown2 = 1
        }

        public GMString Name;
        public GraphTypeEnum GraphType;
        public GMList<GMAnimChannel> Channels;

        
        public void Serialize(GMDataWriter writer)
        {
            writer.WritePointerString(Name);
            writer.Write((uint)GraphType);

            Channels.Serialize(writer);
        }

        public void Unserialize(GMDataReader reader)
        {
            Name = reader.ReadStringPointerObject();
            GraphType = (GraphTypeEnum)reader.ReadUInt32();

            Channels = new GMList<GMAnimChannel>();
            Channels.Unserialize(reader);
        }
    }

    public class GMAnimChannel : GMSerializable
    {
        public enum FunctionTypeEnum
        {
            Linear = 0,
            Smooth = 1,
            Bezier = 2
        }

        public GMString Name;
        public FunctionTypeEnum FunctionType;
        public ushort Iterations;
        public GMList<GMAnimPoint> Points;

        public void Serialize(GMDataWriter writer)
        {
            writer.WritePointerString(Name);
            writer.Write((uint)FunctionType);
            writer.Write(Iterations);

            Points.Serialize(writer);
        }

        public void Unserialize(GMDataReader reader)
        {
            Name = reader.ReadStringPointerObject();
            FunctionType = (FunctionTypeEnum)reader.ReadUInt32();
            Iterations = (ushort)reader.ReadUInt32();

            Points = new GMList<GMAnimPoint>();
            Points.Unserialize(reader);
        }
    }

    public class GMAnimPoint : GMSerializable
    {
        public float X;
        public float Value;

        public float BezierX0; // Bezier only
        public float BezierY0;
        public float BezierX1;
        public float BezierY1;

        public void Serialize(GMDataWriter writer)
        {
            writer.Write(X);
            writer.Write(Value);

            if (writer.VersionInfo.IsNumberAtLeast(2, 3, 1))
            {
                writer.Write(BezierX0);
                writer.Write(BezierY0);
                writer.Write(BezierX1);
                writer.Write(BezierY1);
            }
            else
                writer.Write(0);
        }

        public void Unserialize(GMDataReader reader)
        {
            X = reader.ReadSingle();
            Value = reader.ReadSingle();

            if (reader.ReadUInt32() != 0) // in 2.3 a int with the value of 0 would be set here,
            {                             // it cannot be version 2.3 if this value isn't 0
                reader.VersionInfo.SetNumber(2, 3, 1);
                reader.Offset -= 4;
            }
            else
            {
                if (reader.ReadUInt32() == 0)              // At all points (besides the first one)
                    reader.VersionInfo.SetNumber(2, 3, 1); // if BezierX0 equals to 0 (the above check)
                reader.Offset -= 8;                        // then BezierY0 equals to 0 as well (the current check)
            }

            if (reader.VersionInfo.IsNumberAtLeast(2, 3, 1))
            {
                BezierX0 = reader.ReadSingle();
                BezierY0 = reader.ReadSingle();
                BezierX1 = reader.ReadSingle();
                BezierY1 = reader.ReadSingle();
            }
            else
                reader.Offset += 4;
        }
    }
}
