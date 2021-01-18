using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Models
{
    public class GMTimeline : GMSerializable
    {
        public GMString Name;
        public List<(int, GMPointerList<GMObject.Event.Action>)> Moments;
        
        public void Serialize(GMDataWriter writer)
        {
            writer.WritePointerString(Name);
            writer.Write(Moments.Count);
            foreach (var m in Moments)
            {
                writer.Write(m.Item1);
                writer.WritePointer(m.Item2);
            }
            foreach (var m in Moments)
            {
                writer.WriteObjectPointer(m.Item2);
                m.Item2.Serialize(writer);
            }
        }

        public void Unserialize(GMDataReader reader)
        {
            Name = reader.ReadStringPointerObject();
            Moments = new List<(int, GMPointerList<GMObject.Event.Action>)>();
            for (int i = reader.ReadInt32(); i > 0; i--)
            {
                int time = reader.ReadInt32();
                Moments.Add((time, reader.ReadPointerObject<GMPointerList<GMObject.Event.Action>>()));
            }
        }
    }
}
