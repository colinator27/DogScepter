using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Models
{
    /// <summary>
    /// Contains a GameMaker sequence.
    /// </summary>
    public class GMSequence : GMSerializable
    {
        public enum PlaybackTypeEnum : uint
        {
            Oneshot = 0,
            Loop = 1,
            Pingpong = 2
        }

        public GMString Name;
        public PlaybackTypeEnum PlaybackType;
        public float PlaybackSpeed;
        public GMSprite.AnimSpeedType PlaybackSpeedType;
        public float Length;
        public int OriginX;
        public int OriginY;
        public float Volume;

        public GMList<Keyframe<BroadcastMessage>> BroadcastMessages;
        public GMList<Track> Tracks;
        public Dictionary<int, GMString> FunctionIDs;
        public GMList<Keyframe<Moment>> Moments;

        public void Serialize(GMDataWriter writer)
        {
            writer.WritePointerString(Name);
            writer.Write((uint)PlaybackType);
            writer.Write(PlaybackSpeed);
            writer.Write((uint)PlaybackSpeedType);
            writer.Write(Length);
            writer.Write(OriginX);
            writer.Write(OriginY);
            writer.Write(Volume);

            BroadcastMessages.Serialize(writer);

            Tracks.Serialize(writer);

            writer.Write(FunctionIDs.Count);
            foreach (KeyValuePair<int, GMString> kvp in FunctionIDs)
            {
                writer.Write(kvp.Key);
                writer.WritePointerString(kvp.Value);
            }

            Moments.Serialize(writer);
        }

        public void Deserialize(GMDataReader reader)
        {
            Name = reader.ReadStringPointerObject();
            PlaybackType = (PlaybackTypeEnum)reader.ReadUInt32();
            PlaybackSpeed = reader.ReadSingle();
            PlaybackSpeedType = (GMSprite.AnimSpeedType)reader.ReadUInt32();
            Length = reader.ReadSingle();
            OriginX = reader.ReadInt32();
            OriginY = reader.ReadInt32();
            Volume = reader.ReadSingle();

            BroadcastMessages = new GMList<Keyframe<BroadcastMessage>>();
            BroadcastMessages.Deserialize(reader);

            Tracks = new GMList<Track>();
            Tracks.Deserialize(reader);

            FunctionIDs = new Dictionary<int, GMString>();
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                int key = reader.ReadInt32();
                FunctionIDs[key] = reader.ReadStringPointerObject();
            }

            Moments = new GMList<Keyframe<Moment>>();
            Moments.Deserialize(reader);

        }

        public override string ToString()
        {
            return (Name.Content == string.Empty) ? "Sequence" : $"Sequence: \"{Name.Content}\"";
        }

        public class Keyframe<T> : GMSerializable where T : GMSerializable, new()
        {
            public float Key;
            public float Length;
            public bool Stretch;
            public bool Disabled;
            public Dictionary<int, T> Channels;

            public void Serialize(GMDataWriter writer)
            {
                writer.Write(Key);
                writer.Write(Length);
                writer.WriteWideBoolean(Stretch);
                writer.WriteWideBoolean(Disabled);
                writer.Write(Channels.Count);
                foreach (KeyValuePair<int, T> kvp in Channels)
                {
                    writer.Write(kvp.Key);
                    kvp.Value.Serialize(writer);
                }
            }

            public void Deserialize(GMDataReader reader)
            {
                Key = reader.ReadSingle();
                Length = reader.ReadSingle();
                Stretch = reader.ReadWideBoolean();
                Disabled = reader.ReadWideBoolean();

                int count = reader.ReadInt32();
                Channels = new Dictionary<int, T>();
                for (int i = 0; i < count; i++)
                {
                    int channel = reader.ReadInt32();
                    T data = new T();
                    data.Deserialize(reader);
                    Channels[channel] = data;
                }
            }

            public override string ToString()
            {
                return $"Keyframe of {typeof(T)}";
            }
        }

        public class BroadcastMessage : GMSerializable
        {
            public List<GMString> List;

            public void Serialize(GMDataWriter writer)
            {
                writer.Write(List.Count);
                foreach (GMString str in List)
                {
                    writer.WritePointerString(str);
                }
            }

            public void Deserialize(GMDataReader reader)
            {
                List = new List<GMString>();
                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    List.Add(reader.ReadStringPointerObject());
                }
            }
        }

        public class Track : GMSerializable
        {
            [Flags]
            public enum TraitsEnum
            {
                Unknown1 = 0, // Perhaps Unknown 1 is invisible, and Unknown 2 is locked(?)
                Unknown2 = 1
            }

            public GMString ModelName;
            public GMString Name;
            public int BuiltinName;
            public TraitsEnum Traits;
            public bool IsCreationTrack;
            public List<int> Tags;
            public List<Track> Tracks;
            public TrackKeyframes Keyframes;
            public List<GMSerializable> OwnedResources;
            public List<GMString> OwnedResourceTypes;

            public void Serialize(GMDataWriter writer)
            {
                writer.WritePointerString(ModelName);
                writer.WritePointerString(Name);
                writer.Write(BuiltinName);
                writer.Write((int)Traits);
                writer.WriteWideBoolean(IsCreationTrack);

                writer.Write(Tags.Count);
                writer.Write(OwnedResources.Count);
                writer.Write(Tracks.Count);

                foreach (int i in Tags)
                    writer.Write(i);

                for (int i = 0; i < OwnedResources.Count; i++)
                {
                    writer.WritePointerString(OwnedResourceTypes[i]);
                    OwnedResources[i].Serialize(writer);
                }

                foreach (Track track in Tracks)
                {
                    track.Serialize(writer);
                }

                switch (ModelName.Content)
                {
                    case "GMAudioTrack":
                        {
                            Keyframes.Serialize(writer);
                        }
                        break;
                    case "GMInstanceTrack":
                    case "GMGraphicTrack":
                    case "GMSequenceTrack":
                        {
                            Keyframes.Serialize(writer);
                        }
                        break;
                    case "GMSpriteFramesTrack":
                    case "GMBoolTrack":
                        {
                            Keyframes.Serialize(writer);
                        }
                        break;
                    case "GMStringTrack":
                        {
                            Keyframes.Serialize(writer);
                        }
                        break;
                    case "GMColourTrack":
                    case "GMRealTrack":
                        {
                            Keyframes.Serialize(writer);
                        }
                        break;
                    case "GMGroupTrack":
                        break;
                    default:
                        throw new Exception(string.Format("Unknown sequence \"{0}\" model name.", ModelName.Content));
                }
            }

            public void Deserialize(GMDataReader reader)
            {
                ModelName = reader.ReadStringPointerObject();
                Name = reader.ReadStringPointerObject();
                BuiltinName = reader.ReadInt32();
                Traits = (TraitsEnum)reader.ReadInt32();
                IsCreationTrack = reader.ReadWideBoolean();

                int tagCount = reader.ReadInt32();
                int ownedResourceCount = reader.ReadInt32();
                int trackCount = reader.ReadInt32();

                Tags = new List<int>();
                for (int i = 0; i < tagCount; i++)
                {
                    Tags.Add(reader.ReadInt32());
                }

                OwnedResources = new List<GMSerializable>();
                OwnedResourceTypes = new List<GMString>();
                for (int i = 0; i < ownedResourceCount; i++)
                {
                    GMString str = reader.ReadStringPointerObject();
                    OwnedResourceTypes.Add(str);
                    switch (str.Content)
                    {
                        case "GMAnimCurve":
                            GMAnimCurve curve = new GMAnimCurve();
                            curve.Deserialize(reader);
                            OwnedResources.Add(curve);
                            break;

                        default:
                            reader.Warnings.Add(new GMWarning($"Unknown owned resource type."));
                            break;
                    }
                }

                Tracks = new List<Track>();
                for (int i = 0; i < trackCount; i++)
                {
                    Track track = new Track();
                    track.Deserialize(reader);
                    Tracks.Add(track);
                }

                switch (ModelName.Content)
                {
                    case "GMAudioTrack":
                        {
                            AudioKeyframes keyframes = new AudioKeyframes();
                            keyframes.Deserialize(reader);
                            Keyframes = keyframes;
                        }
                        break;
                    case "GMInstanceTrack":
                    case "GMGraphicTrack":
                    case "GMSequenceTrack":
                        {
                            IDKeyframes keyframes = new IDKeyframes();
                            keyframes.Deserialize(reader);
                            Keyframes = keyframes;
                        }
                        break;
                    case "GMSpriteFramesTrack":
                    case "GMBoolTrack":
                        {
                            ValueKeyframes keyframes = new ValueKeyframes();
                            keyframes.Deserialize(reader);
                            Keyframes = keyframes;
                        }
                        break;
                    case "GMStringTrack":
                        {
                            StringValueKeyframes keyframes = new StringValueKeyframes();
                            keyframes.Deserialize(reader);
                            Keyframes = keyframes;
                        }
                        break;
                    case "GMColourTrack":
                    case "GMRealTrack":
                        {
                            ValueInterpolatedKeyframes keyframes = new ValueInterpolatedKeyframes();
                            keyframes.Deserialize(reader);
                            Keyframes = keyframes;
                        }
                        break;
                    case "GMGroupTrack":
                        break;
                    default:
                        throw new Exception(string.Format("Unknown sequence \"{0}\" model name.", ModelName.Content));
                }
            }

            public class TrackKeyframes : GMSerializable
            {
                public virtual void Serialize(GMDataWriter writer) {}
                public virtual void Deserialize(GMDataReader reader) {}
            }

            public class AudioKeyframes : TrackKeyframes
            {
                public class Data : GMSerializable
                {
                    public int ID;
                    public int Mode;

                    public void Serialize(GMDataWriter writer)
                    {
                        writer.Write(ID);
                        writer.Write((int)0);
                        writer.Write(Mode);
                    }

                    public void Deserialize(GMDataReader reader)
                    {
                        ID = reader.ReadInt32();
                        reader.Offset += 4;
                        Mode = reader.ReadInt32();
                    }
                }

                public GMList<Keyframe<Data>> List;

                public override void Serialize(GMDataWriter writer)
                {
                    List.Serialize(writer);
                }

                public override void Deserialize(GMDataReader reader)
                {
                    List = new GMList<Keyframe<Data>>();
                    List.Deserialize(reader);
                }
            }

            public class IDKeyframes : TrackKeyframes
            {
                public class Data : GMSerializable
                {
                    public int ID;

                    public void Serialize(GMDataWriter writer)
                    {
                        writer.Write(ID);
                    }

                    public void Deserialize(GMDataReader reader)
                    {
                        ID = reader.ReadInt32();
                    }
                }

                public GMList<Keyframe<Data>> List;

                public override void Serialize(GMDataWriter writer)
                {
                    List.Serialize(writer);
                }

                public override void Deserialize(GMDataReader reader)
                {
                    List = new GMList<Keyframe<Data>>();
                    List.Deserialize(reader);
                }
            }

            public class ValueKeyframes : TrackKeyframes
            {
                public class Data : GMSerializable
                {
                    public int Value;

                    public void Serialize(GMDataWriter writer)
                    {
                        writer.Write(Value);
                    }

                    public void Deserialize(GMDataReader reader)
                    {
                        Value = reader.ReadInt32();
                    }
                }

                public GMList<Keyframe<Data>> List;

                public override void Serialize(GMDataWriter writer)
                {
                    List.Serialize(writer);
                }

                public override void Deserialize(GMDataReader reader)
                {
                    List = new GMList<Keyframe<Data>>();
                    List.Deserialize(reader);
                }
            }

            public class StringValueKeyframes : TrackKeyframes
            {
                public GMList<Keyframe<GMString>> List;

                public override void Serialize(GMDataWriter writer)
                {
                    List.Serialize(writer);
                }

                public override void Deserialize(GMDataReader reader)
                {
                    List = new GMList<Keyframe<GMString>>();
                    List.Deserialize(reader);
                }
            }

            public class ValueInterpolatedKeyframes : TrackKeyframes
            {
                public class Data : GMSerializable
                {
                    public int Value;
                    public bool IsCurveEmbedded;
                    public GMAnimCurve AnimCurve;
                    public int AnimCurveID;

                    public void Serialize(GMDataWriter writer)
                    {
                        writer.Write(Value);
                        writer.WriteWideBoolean(IsCurveEmbedded);
                        if (IsCurveEmbedded)
                        {
                            writer.Write((int)-1);
                            AnimCurve.Serialize(writer, false);
                        }
                        else
                        {
                            writer.Write(AnimCurveID);
                        }
                    }

                    public void Deserialize(GMDataReader reader)
                    {
                        Value = reader.ReadInt32();
                        if (reader.ReadWideBoolean())
                        {
                            IsCurveEmbedded = true;
                            if (reader.ReadInt32() != -1)
                                reader.Warnings.Add(new GMWarning("Expected -1 at interpolated value keyframe"));

                            GMAnimCurve curve = new GMAnimCurve();
                            curve.Unserialize(reader, false);
                            AnimCurve = curve;
                        }
                        else
                        {
                            IsCurveEmbedded = false;
                            AnimCurveID = reader.ReadInt32();
                        }
                    }
                }

                public enum InterpolationEnum
                {
                    None,
                    Linear
                }

                public GMList<Keyframe<Data>> List;
                public InterpolationEnum Interpolation;

                public override void Serialize(GMDataWriter writer)
                {
                    writer.Write((int)Interpolation);

                    List.Serialize(writer);
                }

                public override void Deserialize(GMDataReader reader)
                {
                    Interpolation = (InterpolationEnum)reader.ReadInt32();

                    List = new GMList<Keyframe<Data>>();
                    List.Deserialize(reader);
                }
            }

        }

        public class Moment : GMSerializable
        {
            public int InternalCount; // Should be 0 if none, 1 if there's a message?
            public GMString Event;

            public void Serialize(GMDataWriter writer)
            {
                writer.Write(InternalCount);
                if (InternalCount > 0)
                    writer.WritePointerString(Event);
            }

            public void Deserialize(GMDataReader reader)
            {
                InternalCount = reader.ReadInt32();
                if (InternalCount > 0)
                    Event = reader.ReadStringPointerObject();
            }
        }
    }
}
