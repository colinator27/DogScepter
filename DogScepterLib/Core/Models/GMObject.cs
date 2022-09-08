using System.Collections.Generic;
using DogScepterLib.Project.Assets;

namespace DogScepterLib.Core.Models;

/// <summary>
/// Contains a GameMaker object, which can be located in a game world or room.
/// </summary>
public class GMObject : IGMNamedSerializable
{
    /// <summary>
    /// Contains GameMaker object physics properties. All properties will only be used if <see cref="Physics"/> is enabled.
    /// </summary>
    public record struct PhysicsProperties
    {
        /// <summary>
        /// Possible collision shapes a game object can have.
        /// </summary>
        public enum CollisionShape
        {
            /// <summary>
            /// A circular collision shape.
            /// </summary>
            Circle = 0,
            /// <summary>
            /// A rectangular collision shape.
            /// </summary>
            Box = 1,
            /// <summary>
            /// A custom polygonal collision shape.
            /// </summary>
            Custom = 2
        }

        /// <summary>
        /// Whether the game object uses GameMaker's builtin physics engine.
        /// </summary>
        /// <remarks>The default value in GameMaker for this is <see langword="false"/>.</remarks>
        public bool IsEnabled;

        /// <summary>
        /// Whether this game object should act as a sensor fixture, which will cause the game
        /// to ignore all other physical properties of this object, and only react to collision events.
        /// </summary>
        /// <remarks>The default value in GameMaker for this is <see langword="false"/>.</remarks>
        public bool Sensor;

        /// <summary>
        /// The collision shape the game object uses.
        /// </summary>
        /// <remarks>The default value in GameMaker Studio 1 for this is <see cref="CollisionShape.Circle"/> while
        /// in Studio 2 it is <see cref="CollisionShape.Box"/>.</remarks>
        public CollisionShape Shape;

        /// <summary>
        /// The physics density of the game object.
        /// </summary>
        /// <remarks>Density is defined as mass per unit volume, with mass being automatically calculated by
        /// this density value and the unit volume being taken from the surface area of the shape. <br/>
        /// The default value in Gamemaker for this is <c>0.5</c>.</remarks>
        public float Density;

        /// <summary>
        /// Determines how "bouncy" a game object is and is co-dependant on other attributes like <c>Gravity</c> or
        /// <see cref="Friction"/>.
        /// </summary>
        /// <remarks>The default value for this in GameMaker is <c>0.1</c>.</remarks>
        public float Restitution;

        /// <summary>
        /// The collision group this game object belongs to.
        /// </summary>
        /// <remarks>The default value for this in GameMaker is <c>0</c>.</remarks>
        public int Group;

        /// <summary>
        /// The amount of linear damping this game object has, which will gradually slow down moving objects.
        /// </summary>
        /// <remarks>The default value for this in GameMaker is <c>0.1</c></remarks>
        public float LinearDamping;

        /// <summary>
        /// The amount of angular damping this game object has, which will slow down rotating objects.
        /// </summary>
        /// <remarks>The default value for this in GameMaker is <c>0.1</c>.</remarks>
        public float AngularDamping;

        /// <summary>
        /// The list of vertices used for <see cref="CollisionShape"/>.<see cref="CollisionShape.Custom"/>.
        /// </summary>
        public List<PhysicsVertex> Vertices;

        /// <summary>
        /// The amount of friction this game object has, which will cause a loss of momentum during collisions.
        /// </summary>
        /// <remarks>The default value for this in GameMaker is <c>0.2</c>.</remarks>
        public float Friction;

        /// <summary>
        /// Whether the game object should use physics simulation on object creation.
        /// </summary>
        /// <remarks>The default value for this in GameMaker is <see langword="true"/>.</remarks>
        public bool IsAwake;

        /// <summary>
        /// Whether the game object should be kinematic, which makes it unaffected by collisions and other physics properties
        /// Will only be used if <see cref="Density"/> is set to <c>0</c>.
        /// </summary>
        /// <remarks>The default value for this in GameMaker is <see langword="false"/>.</remarks>
        public bool IsKinematic;

        /// <summary>
        /// An explicit cast from a <see cref="AssetObject"/>.<see cref="AssetObject.PhysicsProperties"/>
        /// struct to a <see cref="PhysicsProperties"/>.
        /// </summary>
        /// <param name="physicsProperties">The physics properties as
        /// <see cref="AssetObject"/>.<see cref="AssetObject.PhysicsProperties"/>.</param>
        /// <returns>Physics properties as <see cref="PhysicsProperties"/>.</returns>
        public static explicit operator PhysicsProperties(AssetObject.PhysicsProperties physicsProperties)
        {
            PhysicsProperties newPhysics = new PhysicsProperties
            {
                IsEnabled = physicsProperties.IsEnabled,
                Sensor = physicsProperties.Sensor,
                Shape = physicsProperties.Shape,
                Density = physicsProperties.Density,
                Restitution = physicsProperties.Restitution,
                Group = physicsProperties.Group,
                LinearDamping = physicsProperties.LinearDamping,
                AngularDamping = physicsProperties.AngularDamping,
                Friction = physicsProperties.Friction,
                IsAwake = physicsProperties.IsAwake,
                IsKinematic = physicsProperties.IsKinematic,
                Vertices = new()
            };
            foreach (AssetObject.PhysicsVertex v in physicsProperties.Vertices)
                newPhysics.Vertices.Add(new PhysicsVertex { X = v.X, Y = v.Y });

            return newPhysics;
        }
    }

    /// <summary>
    /// The name of the GameMaker object.
    /// </summary>
    public GMString Name { get; set; }

    /// <summary>
    /// The ID of the <see cref="GMSprite"/> this game object uses.
    /// </summary>
    public int SpriteID;

    /// <summary>
    /// Whether the game object is visible when the room starts.
    /// </summary>
    /// <remarks>The default value in GameMaker for this is <see langword="true"/>.</remarks>
    public bool Visible;

    /// <summary>
    /// Whether the game object is marked as "managed."
    /// This is used for rollback multiplayer functionality since mid-2022 betas, however the field exists in 2022.5+.
    /// </summary>
    public bool Managed;

    /// <summary>
    /// Whether the game object is solid.
    /// </summary>
    /// <remarks>This will cause GameMaker to resolve any collisions,
    /// by moving the instance back to where it was before the collision, before executing other collision code. <br/>
    /// The default value in GameMaker for this is <see langword="false"/>.</remarks>
    public bool Solid;

    /// <summary>
    /// The depth level of the game object. Only used in GameMaker Studio 1.
    /// </summary>
    /// <remarks>Objects with a higher depth are drawn first, while objects with a lower depth being drawn last.
    /// So an object with a depth level of 100 will appear behind an object with a depth level of -100. <br/>
    /// The default value in GameMaker for this is <c>0</c>. <br/><br/>
    ///
    /// In GameMaker Studio 2, an object's depth level is instead determined by the depth level of
    /// the <see cref="GMRoom.Layer"/> it is placed on.</remarks>
    public int Depth;

    /// <summary>
    /// Whether the game object is persistent between rooms.
    /// </summary>
    /// <remarks>The default value in GameMaker for this is <see langword="false"/>.</remarks>
    public bool Persistent;

    /// <summary>
    /// The ID of the parent <see cref="GMObject"/> this game object is inheriting from.
    /// TODO: what if it doesn't have a parent?
    /// </summary>
    public int ParentObjectID;

    /// <summary>
    /// The ID of the <see cref="GMSprite"/> this game object is using as a texture mask.
    /// TODO: what if it doesn't have one/uses the same as sprite?
    /// </summary>
    public int MaskSpriteID;

    /// <summary>
    /// The physics properties of this <see cref="GMObject"/>.
    /// </summary>
    public PhysicsProperties Physics;

    /// <summary>
    /// The events that the game object has.
    /// </summary>
    public GMUniquePointerList<GMUniquePointerList<Event>> Events;

    public void Serialize(GMDataWriter writer)
    {
        writer.WritePointerString(Name);
        writer.Write(SpriteID);
        writer.WriteWideBoolean(Visible);
        if (writer.VersionInfo.IsVersionAtLeast(2022, 5))
            writer.WriteWideBoolean(Managed);
        writer.WriteWideBoolean(Solid);
        writer.Write(Depth);
        writer.WriteWideBoolean(Persistent);
        writer.Write(ParentObjectID);
        writer.Write(MaskSpriteID);
        writer.WriteWideBoolean(Physics.IsEnabled);
        writer.WriteWideBoolean(Physics.Sensor);
        writer.Write((int)Physics.Shape);
        writer.Write(Physics.Density);
        writer.Write(Physics.Restitution);
        writer.Write(Physics.Group);
        writer.Write(Physics.LinearDamping);
        writer.Write(Physics.AngularDamping);
        writer.Write(Physics.Vertices.Count);
        writer.Write(Physics.Friction);
        writer.WriteWideBoolean(Physics.IsAwake);
        writer.WriteWideBoolean(Physics.IsKinematic);
        foreach (PhysicsVertex v in Physics.Vertices)
            v.Serialize(writer);
        Events.Serialize(writer);
    }

    public void Deserialize(GMDataReader reader)
    {
        Name = reader.ReadStringPointerObject();
        SpriteID = reader.ReadInt32();
        Visible = reader.ReadWideBoolean();
        if (reader.VersionInfo.IsVersionAtLeast(2022, 5))
            Managed = reader.ReadWideBoolean();
        Solid = reader.ReadWideBoolean();
        Depth = reader.ReadInt32();
        Persistent = reader.ReadWideBoolean();
        ParentObjectID = reader.ReadInt32();
        MaskSpriteID = reader.ReadInt32();
        Physics.IsEnabled = reader.ReadWideBoolean();
        Physics.Sensor = reader.ReadWideBoolean();
        Physics.Shape = (PhysicsProperties.CollisionShape)reader.ReadInt32();
        Physics.Density = reader.ReadSingle();
        Physics.Restitution = reader.ReadSingle();
        Physics.Group = reader.ReadInt32();
        Physics.LinearDamping = reader.ReadSingle();
        Physics.AngularDamping = reader.ReadSingle();
        int vertexCount = reader.ReadInt32();
        Physics.Friction = reader.ReadSingle();
        Physics.IsAwake = reader.ReadWideBoolean();
        Physics.IsKinematic = reader.ReadWideBoolean();
        Physics.Vertices = new List<PhysicsVertex>(vertexCount);
        for (int i = vertexCount; i > 0; i--)
        {
            PhysicsVertex v = new PhysicsVertex();
            v.Deserialize(reader);
            Physics.Vertices.Add(v);
        }
        Events = new GMUniquePointerList<GMUniquePointerList<Event>>();
        Events.Deserialize(reader);
    }

    public override string ToString()
    {
        return $"Object: \"{Name.Content}\"";
    }

    /// <summary>
    /// Contains a physics vertex which is used for <see cref="PhysicsProperties.CollisionShape"/>.<see cref="PhysicsProperties.CollisionShape.Custom"/>.
    /// </summary>
    public class PhysicsVertex : IGMSerializable
    {
        /// <summary>
        /// The X position of the physics vertex.
        /// </summary>
        public float X;

        /// <summary>
        /// The Y position of the physics vertex.
        /// </summary>
        public float Y;

        public void Serialize(GMDataWriter writer)
        {
            writer.Write(X);
            writer.Write(Y);
        }

        public void Deserialize(GMDataReader reader)
        {
            X = reader.ReadSingle();
            Y = reader.ReadSingle();
        }
    }

    /// <summary>
    /// An event that a <see cref="GMObject"/> uses.
    /// </summary>
    public class Event : IGMSerializable
    {
        /// <summary>
        /// The subtype of this event.
        /// </summary>
        /// <remarks>GameMaker suffixes <see cref="GMCode"/>.<see cref="GMCode.Name"/>, which
        /// <see cref="Action"/>.<see cref="Action.CodeID"/> references, with this ID.</remarks>
        public int Subtype;

        /// <summary>
        /// A list of <see cref="Action"/>s this event executes.
        /// </summary>
        /// <remarks>Possible to have more than one entry on <i>very</i> old GameMaker versions, due to Drag-and-Drop which didn't
        /// convert it directly to GML when compiling. Should usually only ever have one entry.</remarks>
        public GMUniquePointerList<Action> Actions;

        public void Serialize(GMDataWriter writer)
        {
            writer.Write(Subtype);
            Actions.Serialize(writer);
        }

        public void Deserialize(GMDataReader reader)
        {
            Subtype = reader.ReadInt32();
            Actions = new GMUniquePointerList<Action>();
            Actions.Deserialize(reader);
        }

        /// <summary>
        /// An action that an <see cref="Event"/> can execute.
        /// </summary>
        public class Action : IGMSerializable
        {
            //TODO: a lot of unknown values, from pre studio DnD era. Nowadays, DnD blocks are compiled to bytecode.
            // The attribute names have been taken from the 1.X project files.
            public int LibID;
            public int ID;
            public int Kind;
            public bool UseRelative;
            public bool IsQuestion;
            public bool UseApplyTo;
            public int ExeType;
            public GMString ActionName;

            /// <summary>
            /// The ID of the <see cref="GMCode"/> that will be executed.
            /// </summary>
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

            public void Deserialize(GMDataReader reader)
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