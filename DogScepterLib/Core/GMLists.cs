using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace DogScepterLib.Core
{
    // Callbacks for before/after serializing each element, for padding/etc.
    public delegate void ListSerialize(GMDataWriter writer, int index, int count);
    public delegate void ListDeserialize(GMDataReader reader, int index, int count);

    // Callbacks for reading/writing each element, in special scenarios
    public delegate void ListSerializeElement(GMDataWriter writer, IGMSerializable elem);
    public delegate IGMSerializable ListDeserializeElement(GMDataReader reader, bool notLast);

    /// <summary>
    /// Basic array-like list type in a GameMaker data file.
    /// </summary>
    public class GMList<T> : List<T>, IGMSerializable where T : IGMSerializable, new()
    {
        /// <summary>
        /// Initializes an empty <see cref="GMList{T}"/>.
        /// </summary>
        public GMList()
        {
        }

        /// <summary>
        /// Initializes an empty <see cref="GMList{T}"/> with a specified capacity.
        /// </summary>
        /// <param name="capacity">How many elements this <see cref="GMList{T}"/> should be able to hold.</param>
        public GMList(int capacity) : base(capacity)
        {
        }

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

        public virtual void Deserialize(GMDataReader reader, ListDeserialize before = null,
                                                             ListDeserialize after = null,
                                                             ListDeserializeElement elemReader = null)
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
                    elem.Deserialize(reader);
                }
                else
                    elem = (T)elemReader(reader, (i + 1 != count));
                Add(elem);

                after?.Invoke(reader, i, count);
            }
        }

        public virtual void Deserialize(GMDataReader reader)
        {
            Deserialize(reader, null, null, null);
        }
    }

    /// <summary>
    /// A list of pointers to objects, forming a list, in a GameMaker data file.
    /// </summary>
    public class GMPointerList<T> : GMList<T> where T : IGMSerializable, new()
    {
        /// <summary>
        /// TODO
        /// </summary>
        public bool UsePointerMap = true;

        /// <summary>
        /// Initializes an empty <see cref="GMPointerList{T}"/>.
        /// </summary>
        public GMPointerList()
        {
        }

        /// <summary>
        /// Initializes an empty <see cref="GMPointerList{T}"/> with a specified capacity.
        /// </summary>
        /// <param name="capacity">How many elements this <see cref="GMPointerList{T}"/> should be able to hold.</param>
        public GMPointerList(int capacity) : base(capacity)
        {
        }

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

        private static IGMSerializable DoReadPointerObject(GMDataReader reader, bool notLast)
        {
            return reader.ReadPointerObject<T>(reader.ReadInt32(), notLast);
        }

        private static IGMSerializable DoReadPointerObjectUnique(GMDataReader reader, bool notLast)
        {
            return reader.ReadPointerObjectUnique<T>(reader.ReadInt32(), notLast);
        }

        public override void Deserialize(GMDataReader reader, ListDeserialize before = null,
                                                              ListDeserialize after = null,
                                                              ListDeserializeElement elemReader = null)
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

        public override void Deserialize(GMDataReader reader)
        {
            Deserialize(reader, null, null, null);
        }
    }

    /// <summary>
    /// A list of pointers to objects, forming a list, in a GameMaker data file. <br/>
    /// The difference to the normal <see cref="GMPointerList{T}"/> is, that this one's objects are
    /// specifically specified to not be adjacent, therefore the offset is reset at the end
    /// to the offset after the final pointer. Also, writing does not serialize actual objects.
    /// </summary>
    public class GMRemotePointerList<T> : GMList<T> where T : IGMSerializable, new()
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

        public override void Deserialize(GMDataReader reader, ListDeserialize before = null,
                                                              ListDeserialize after = null,
                                                              ListDeserializeElement elemReader = null)
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

        public override void Deserialize(GMDataReader reader)
        {
            Deserialize(reader, null, null, null);
        }
    }

    /// <summary>
    /// A list of pointers to objects, forming a list, in a GameMaker data file. <br/>
    /// This variant automatically sets <see cref="GMPointerList{T}.UsePointerMap"/> in the base class to false.
    /// </summary>
    public class GMUniquePointerList<T> : GMPointerList<T> where T : IGMSerializable, new()
    {
        /// <summary>
        /// Initializes an empty <see cref="GMUniquePointerList{T}"/>.
        /// </summary>
        public GMUniquePointerList()
        {
            UsePointerMap = false;
        }

        /// <summary>
        /// Initializes an empty <see cref="GMUniquePointerList{T}"/> with a specified capacity.
        /// </summary>
        /// <param name="capacity">How many elements this <see cref="GMUniquePointerList{T}"/> should be able to hold.</param>
        public GMUniquePointerList(int capacity) : base(capacity)
        {
            UsePointerMap = false;
        }
    }
}
