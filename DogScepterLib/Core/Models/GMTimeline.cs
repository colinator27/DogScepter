using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Models
{
    /// <summary>
    /// Contains a GameMaker timeline.
    /// </summary>
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

        public void Deserialize(GMDataReader reader)
        {
            Name = reader.ReadStringPointerObject();
            int count = reader.ReadInt32();
            Moments = new List<(int, GMPointerList<GMObject.Event.Action>)>(count);
            for (int i = count; i > 0; i--)
            {
                int time = reader.ReadInt32();
                Moments.Add((time, reader.ReadPointerObjectUnique<GMPointerList<GMObject.Event.Action>>()));
            }
        }

        public override string ToString()
        {
            return $"Timeline: \"{Name.Content}\"";
        }
    }
}
