using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace DogScepterLib.Core
{
    // Callbacks for before/after serializing each element, for padding/etc.
    public delegate void ListSerialize(GMDataWriter writer, int index, int count);
    public delegate void ListUnserialize(GMDataReader reader, int index, int count);

    // Callbacks for reading/writing each element, in special scenarios
    public delegate void ListSerializeElement(GMDataWriter writer, GMSerializable elem);
    public delegate GMSerializable ListUnserializeElement(GMDataReader reader, bool notLast);

    /// <summary>
    /// Basic array-like list type in data file
    /// </summary>
    public class GMList<T> : List<T>, GMSerializable where T : GMSerializable, new()
    {
        public virtual void Serialize(GMDataWriter writer, ListSerialize before = null,
                                                           ListSerialize after = null,
                                                           ListSerializeElement elemWriter = null)
        {
            writer.Write(Count);
            for (int i = 0; i < Count; i++)
            {
                before?.Invoke(writer, i, Count);

                // Write the current element in the list
                if (elemWriter == null)
                    this[i].Serialize(writer);
                else
                    elemWriter(writer, this[i]);

                after?.Invoke(writer, i, Count);
            }
        }

        public virtual void Serialize(GMDataWriter writer)
        {
            Serialize(writer, null, null, null);
        }

        public virtual void Unserialize(GMDataReader reader, ListUnserialize before = null,
                                                             ListUnserialize after = null,
                                                             ListUnserializeElement elemReader = null)
        {
            // Read the element count and begin reading elements
            int count = reader.ReadInt32();
            Capacity = count;
            for (int i = 0; i < count; i++)
            {
                before?.Invoke(reader, i, count);

                // Read the current element and add it to the list
                T elem;
                if (elemReader == null)
                {
                    elem = new T();
                    elem.Unserialize(reader);
                }
                else
                    elem = (T)elemReader(reader, (i + 1 != count));
                Add(elem);

                after?.Invoke(reader, i, count);
            }
        }

        public virtual void Unserialize(GMDataReader reader)
        {
            Unserialize(reader, null, null, null);
        }
    }

    /// <summary>
    /// A list of pointers to objects, forming a list, in the data file
    /// </summary>
    public class GMPointerList<T> : GMList<T> where T : GMSerializable, new()
    {
        public bool UsePointerMap = true;

        public void Serialize(GMDataWriter writer, ListSerialize before = null,
                                                   ListSerialize after = null,
                                                   ListSerializeElement elemWriter = null,
                                                   ListSerializeElement elemPointerWriter = null)
        {
            writer.Write(Count);

            // Write each element's pointer
            for (int i = 0; i < Count; i++)
            {
                if (elemPointerWriter == null)
                    writer.WritePointer(this[i]);
                else
                    elemPointerWriter(writer, this[i]);
            }

            // Write each element
            for (int i = 0; i < Count; i++)
            {
                before?.Invoke(writer, i, Count);

                // Write the current element in the list
                if (elemWriter == null)
                {
                    writer.WriteObjectPointer(this[i]);
                    this[i].Serialize(writer);
                }
                else
                    elemWriter(writer, this[i]);

                after?.Invoke(writer, i, Count);
            }
        }

        public override void Serialize(GMDataWriter writer, ListSerialize before = null,
                                                            ListSerialize after = null,
                                                            ListSerializeElement elemWriter = null)
        {
            Serialize(writer, before, after, elemWriter, null);
        }

        public override void Serialize(GMDataWriter writer)
        {
            Serialize(writer, null, null, null);
        }

        private static GMSerializable DoReadPointerObject(GMDataReader reader, bool notLast)
        {
            return reader.ReadPointerObject<T>(reader.ReadInt32(), notLast);
        }

        private static GMSerializable DoReadPointerObjectUnique(GMDataReader reader, bool notLast)
        {
            return reader.ReadPointerObjectUnique<T>(reader.ReadInt32(), notLast);
        }

        public override void Unserialize(GMDataReader reader, ListUnserialize before = null,
                                                              ListUnserialize after = null,
                                                              ListUnserializeElement elemReader = null)
        {
            // Define a default pointer reader if none is set
            if (elemReader == null)
            {
                if (UsePointerMap)
                    elemReader = DoReadPointerObject;
                else
                    elemReader = DoReadPointerObjectUnique;
            }

            // Read the element count and begin reading elements
            int count = reader.ReadInt32();
            Capacity = count;
            for (int i = 0; i < count; i++)
            {
                before?.Invoke(reader, i, count);

                // Read the current element and add it to the list
                Add((T)elemReader(reader, i + 1 != count));

                after?.Invoke(reader, i, count);
            }
        }

        public override void Unserialize(GMDataReader reader)
        {
            Unserialize(reader, null, null, null);
        }
    }

    /// <summary>
    /// A list of pointers to objects, forming a list, in the data file
    /// The difference between the normal PointerList is that this one's objects are
    /// specifically specified to not be adjacent, therefore the offset is reset at the end
    /// to the offset after the final pointer. Also, writing does not serialize actual objects.
    /// </summary>
    public class GMRemotePointerList<T> : GMList<T> where T : GMSerializable, new()
    {

        public void Serialize(GMDataWriter writer, ListSerialize before = null,
                                                   ListSerialize after = null,
                                                   ListSerializeElement elemWriter = null,
                                                   ListSerializeElement elemPointerWriter = null)
        {
            writer.Write(Count);

            // Write each element's pointer
            for (int i = 0; i < Count; i++)
            {
                if (elemPointerWriter == null)
                    writer.WritePointer(this[i]);
                else
                    elemPointerWriter(writer, this[i]);
            }
        }

        public override void Serialize(GMDataWriter writer, ListSerialize before = null,
                                                            ListSerialize after = null,
                                                            ListSerializeElement elemWriter = null)
        {
            Serialize(writer, before, after, elemWriter, null);
        }

        public override void Serialize(GMDataWriter writer)
        {
            Serialize(writer, null, null, null);
        }

        public override void Unserialize(GMDataReader reader, ListUnserialize before = null,
                                                              ListUnserialize after = null,
                                                              ListUnserializeElement elemReader = null)
        {
            // Read the element count and begin reading elements
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                before?.Invoke(reader, i, Count);

                // Read the current element and add it to the list
                T elem;
                if (elemReader == null)
                {
                    elem = reader.ReadPointerObject<T>(reader.ReadInt32(), true);
                }
                else
                {
                    elem = (T)elemReader(reader, true);
                }
                Add(elem);

                after?.Invoke(reader, i, Count);
            }
        }

        public override void Unserialize(GMDataReader reader)
        {
            Unserialize(reader, null, null, null);
        }
    }

    /// <summary>
    /// A list of pointers to objects, forming a list, in the data file.
    /// This variant automatically sets UsePointerMap in the base class to false. 
    /// </summary>
    public class GMUniquePointerList<T> : GMPointerList<T> where T : GMSerializable, new()
    {
        public GMUniquePointerList() : base()
        {
            UsePointerMap = false;
        }
    }
}
