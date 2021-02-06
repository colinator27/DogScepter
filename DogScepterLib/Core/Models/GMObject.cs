using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Models
{
    /// <summary>
    /// Contains a GameMaker object (as in the game world/room)
    /// </summary>
    public class GMObject : GMSerializable
    {
        public enum CollisionShape : int
        {
            Circle = 0,
            Box = 1,
            Custom = 2
        }

        public GMString Name;
        public int SpriteID;
        public bool Visible;
        public bool Solid;
        public int Depth;
        public bool Persistent;
        public int ParentObjectID;
        public int MaskSpriteID;
        public bool Physics;
        public bool PhysicsSensor;
        public CollisionShape PhysicsShape;
        public float PhysicsDensity;
        public float PhysicsRestitution;
        public int PhysicsGroup;
        public float PhysicsLinearDamping;
        public float PhysicsAngularDamping;
        public List<PhysicsVertex> PhysicsVertices;
        public float PhysicsFriction;
        public bool PhysicsAwake;
        public bool PhysicsKinematic;
        public GMPointerList<GMPointerList<Event>> Events;

        public void Serialize(GMDataWriter writer)
        {
            writer.WritePointerString(Name);
            writer.Write(SpriteID);
            writer.WriteWideBoolean(Visible);
            writer.WriteWideBoolean(Solid);
            writer.Write(Depth);
            writer.WriteWideBoolean(Persistent);
            writer.Write(ParentObjectID);
            writer.Write(MaskSpriteID);
            writer.WriteWideBoolean(Physics);
            writer.WriteWideBoolean(PhysicsSensor);
            writer.Write((int)PhysicsShape);
            writer.Write(PhysicsDensity);
            writer.Write(PhysicsRestitution);
            writer.Write(PhysicsGroup);
            writer.Write(PhysicsLinearDamping);
            writer.Write(PhysicsAngularDamping);
            writer.Write(PhysicsVertices.Count);
            writer.Write(PhysicsFriction);
            writer.WriteWideBoolean(PhysicsAwake);
            writer.WriteWideBoolean(PhysicsKinematic);
            foreach (PhysicsVertex v in PhysicsVertices)
                v.Serialize(writer);
            Events.Serialize(writer);
        }

        public void Unserialize(GMDataReader reader)
        {
            Name = reader.ReadStringPointerObject();
            SpriteID = reader.ReadInt32();
            Visible = reader.ReadWideBoolean();
            Solid = reader.ReadWideBoolean();
            Depth = reader.ReadInt32();
            Persistent = reader.ReadWideBoolean();
            ParentObjectID = reader.ReadInt32();
            MaskSpriteID = reader.ReadInt32();
            Physics = reader.ReadWideBoolean();
            PhysicsSensor = reader.ReadWideBoolean();
            PhysicsShape = (CollisionShape)reader.ReadInt32();
            PhysicsDensity = reader.ReadSingle();
            PhysicsRestitution = reader.ReadSingle();
            PhysicsGroup = reader.ReadInt32();
            PhysicsLinearDamping = reader.ReadSingle();
            PhysicsAngularDamping = reader.ReadSingle();
            int vertexCount = reader.ReadInt32();
            PhysicsFriction = reader.ReadSingle();
            PhysicsAwake = reader.ReadWideBoolean();
            PhysicsKinematic = reader.ReadWideBoolean();
            PhysicsVertices = new List<PhysicsVertex>();
            for (int i = vertexCount; i > 0; i--)
            {
                PhysicsVertex v = new PhysicsVertex();
                v.Unserialize(reader);
                PhysicsVertices.Add(v);
            }
            Events = new GMPointerList<GMPointerList<Event>>();
            Events.Unserialize(reader);
        }

        public override string ToString()
        {
            return $"Object: \"{Name.Content}\"";
        }

        public class PhysicsVertex : GMSerializable
        {
            public float X, Y;

            public void Serialize(GMDataWriter writer)
            {
                writer.Write(X);
                writer.Write(Y);
            }

            public void Unserialize(GMDataReader reader)
            {
                X = reader.ReadSingle();
                Y = reader.ReadSingle();
            }
        }

        public class Event : GMSerializable
        {
            public int Subtype;
            public GMPointerList<Action> Actions;

            public void Serialize(GMDataWriter writer)
            {
                writer.Write(Subtype);
                Actions.Serialize(writer);
            }

            public void Unserialize(GMDataReader reader)
            {
                Subtype = reader.ReadInt32();
                Actions = new GMPointerList<Action>();
                Actions.Unserialize(reader);
            }

            public class Action : GMSerializable
            {
                public int LibID;
                public int ID;
                public int Kind;
                public bool UseRelative;
                public bool IsQuestion;
                public bool UseApplyTo;
                public int ExeType;
                public GMString ActionName;
                public int CodeID;
                public int ArgumentCount;
                public int Who;
                public bool Relative;
                public bool IsNot;

                public void Serialize(GMDataWriter writer)
                {
                    writer.Write(LibID);
                    writer.Write(ID);
                    writer.Write(Kind);
                    writer.WriteWideBoolean(UseRelative);
                    writer.WriteWideBoolean(IsQuestion);
                    writer.WriteWideBoolean(UseApplyTo);
                    writer.Write(ExeType);
                    writer.WritePointerString(ActionName);
                    writer.Write(CodeID);
                    writer.Write(ArgumentCount);
                    writer.Write(Who);
                    writer.WriteWideBoolean(Relative);
                    writer.WriteWideBoolean(IsNot);
                    writer.Write(0);
                }

                public void Unserialize(GMDataReader reader)
                {
                    LibID = reader.ReadInt32();
                    ID = reader.ReadInt32();
                    Kind = reader.ReadInt32();
                    UseRelative = reader.ReadWideBoolean();
                    IsQuestion = reader.ReadWideBoolean();
                    UseApplyTo = reader.ReadWideBoolean();
                    ExeType = reader.ReadInt32();
                    ActionName = reader.ReadStringPointerObject();
                    CodeID = reader.ReadInt32();
                    ArgumentCount = reader.ReadInt32();
                    Who = reader.ReadInt32();
                    Relative = reader.ReadWideBoolean();
                    IsNot = reader.ReadWideBoolean();
                    if (reader.ReadInt32() != 0)
                        reader.Warnings.Add(new GMWarning("expected 0 in OBJT"));
                }
            }
        }
    }
}
