using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace DogScepterLib.Core
{
    // Callbacks for before/after serializing each element, for padding/etc.
    public delegate void ListSerialize(GMDataWriter writer);
    public delegate void ListUnserialize(GMDataReader reader);

    // Callbacks for reading/writing each element, in special scenarios
    public delegate void ListSerializeElement(GMDataWriter writer, GMSerializable elem);
    public delegate GMSerializable ListUnserializeElement(GMDataReader reader, bool last);

    /// <summary>
    /// Basic array-like list type in data file
    /// </summary>
    public class GMList<T> : ObservableCollection<T>, GMSerializable where T : GMSerializable, new()
    {
        public virtual void Serialize(GMDataWriter writer, ListSerialize before = null,
                                                           ListSerialize after = null,
                                                           ListSerializeElement elemWriter = null)
        {
            writer.Write(Count);
            for (int i = 0; i < Count; i++)
            {
                before?.Invoke(writer);

                // Write the current element in the list
                if (elemWriter == null)
                    this[i].Serialize(writer);
                else
                    elemWriter(writer, this[i]);

                after?.Invoke(writer);
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
            for (int i = 0; i < count; i++)
            {
                before?.Invoke(reader);

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

                after?.Invoke(reader);
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
                before?.Invoke(writer);

                // Write the current element in the list
                if (elemWriter == null)
                {
                    writer.WriteObjectPointer(this[i]);
                    this[i].Serialize(writer);
                }
                else
                    elemWriter(writer, this[i]);

                after?.Invoke(writer);
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
                before?.Invoke(reader);

                // Read the current element and add it to the list
                T elem;
                if (elemReader == null)
                {
                    elem = reader.ReadPointerObject<T>(reader.ReadInt32(), before, after, (i + 1 != count));
                } else
                {
                    before?.Invoke(reader);
                    elem = (T)elemReader(reader, (i + 1 != count));
                    after?.Invoke(reader);
                }
                Add(elem);

                after?.Invoke(reader);
            }
        }

        public override void Unserialize(GMDataReader reader)
        {
            Unserialize(reader, null, null, null);
        }
    }
}
