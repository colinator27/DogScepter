using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Models
{
    /// <summary>
    /// Contains a GameMaker room.
    /// </summary>
    public class GMRoom : IGMNamedSerializable
    {
        [Flags]
        public enum RoomFlags : int
        {
            EnableViews = 1,
            ShowColor = 2,
            ClearDisplayBuffer = 4
        }

        public GMString Name { get; set; }
        public GMString Caption;
        public int Width, Height;
        public int Speed;
        public bool Persistent;
        public int BackgroundColor;
        public bool DrawBackgroundColor;
        public int CreationCodeID;
        public RoomFlags Flags;
        public GMUniquePointerList<Background> Backgrounds;
        public GMUniquePointerList<View> Views;
        public GMUniquePointerList<GameObject> GameObjects;
        public GMUniquePointerList<Tile> Tiles;
        public bool Physics;
        public int Top, Left, Right, Bottom;
        public float GravityX, GravityY;
        public float PixelsToMeters;

        // GMS2+ only
        public GMUniquePointerList<Layer> Layers;

        // GMS2.3+ only
        public List<int> SequenceIDs;

        public void Serialize(GMDataWriter writer)
        {
            writer.WritePointerString(Name);
            writer.WritePointerString(Caption);
            writer.Write(Width); writer.Write(Height);
            writer.Write(Speed);
            writer.WriteWideBoolean(Persistent);
            writer.Write(BackgroundColor);
            writer.WriteWideBoolean(DrawBackgroundColor);
            writer.Write(CreationCodeID);
            int flags = (int)Flags;
            if (writer.VersionInfo.IsVersionAtLeast(2, 3))
                flags |= 0x30000;
            else if (writer.VersionInfo.IsVersionAtLeast(2))
                flags |= 0x20000;
            writer.Write(flags);
            writer.WritePointer(Backgrounds);
            writer.WritePointer(Views);
            writer.WritePointer(GameObjects);
            writer.WritePointer(Tiles);
            writer.WriteWideBoolean(Physics);
            writer.Write(Top); writer.Write(Left);
            writer.Write(Right); writer.Write(Bottom);
            writer.Write(GravityX); writer.Write(GravityY);
            writer.Write(PixelsToMeters);
            int sequencePatch = -1;
            if (writer.VersionInfo.IsVersionAtLeast(2))
            {
                writer.WritePointer(Layers);
                if (writer.VersionInfo.IsVersionAtLeast(2, 3))
                {
                    sequencePatch = writer.Offset;
                    writer.Write(0);
                }
            }

            writer.WriteObjectPointer(Backgrounds);
            Backgrounds.Serialize(writer);
            writer.WriteObjectPointer(Views);
            Views.Serialize(writer);
            writer.WriteObjectPointer(GameObjects);
            GameObjects.Serialize(writer);
            writer.WriteObjectPointer(Tiles);
            Tiles.Serialize(writer);
            if (writer.VersionInfo.IsVersionAtLeast(2))
            {
                writer.WriteObjectPointer(Layers);
                Layers.Serialize(writer);
                if (writer.VersionInfo.IsVersionAtLeast(2, 3))
                {
                    // Patch and write sequence IDs
                    int returnTo = writer.Offset;
                    writer.Offset = sequencePatch;
                    writer.Write(returnTo);
                    writer.Offset = returnTo;

                    writer.Write(SequenceIDs.Count);
                    foreach (int i in SequenceIDs)
                        writer.Write(i);
                }
            }
        }

        public void Deserialize(GMDataReader reader)
        {
            Name = reader.ReadStringPointerObject();
            Caption = reader.ReadStringPointerObject();
            Width = reader.ReadInt32(); Height = reader.ReadInt32();
            Speed = reader.ReadInt32();
            Persistent = reader.ReadWideBoolean();
            BackgroundColor = reader.ReadInt32();
            DrawBackgroundColor = reader.ReadWideBoolean();
            CreationCodeID = reader.ReadInt32();
            int flags = reader.ReadInt32();
            if (reader.VersionInfo.IsVersionAtLeast(2, 3))
                flags &= ~0x30000;
            else if (reader.VersionInfo.IsVersionAtLeast(2))
                flags &= ~0x20000;
            Flags = (RoomFlags)flags;
            Backgrounds = reader.ReadPointerObjectUnique<GMUniquePointerList<Background>>();
            Views = reader.ReadPointerObjectUnique<GMUniquePointerList<View>>();
            int gameObjectListPtr = reader.ReadInt32(); // read this later
            int tilePtr = reader.ReadInt32();
            Tiles = reader.ReadPointerObjectUnique<GMUniquePointerList<Tile>>(tilePtr);
            Physics = reader.ReadWideBoolean();
            Top = reader.ReadInt32(); Left = reader.ReadInt32();
            Right = reader.ReadInt32(); Bottom = reader.ReadInt32();
            GravityX = reader.ReadSingle(); GravityY = reader.ReadSingle();
            PixelsToMeters = reader.ReadSingle();
            if (reader.VersionInfo.IsVersionAtLeast(2))
            {
                Layers = reader.ReadPointerObjectUnique<GMUniquePointerList<Layer>>();
                if (reader.VersionInfo.IsVersionAtLeast(2, 3))
                {
                    // Read sequence ID list
                    reader.Offset = reader.ReadInt32();
                    int seqIdCount = reader.ReadInt32();
                    SequenceIDs = new List<int>(seqIdCount);
                    for (int i = seqIdCount; i > 0; i--)
                        SequenceIDs.Add(reader.ReadInt32());
                }
            }

            // Handle reading game objects, which change lengths in 2.2.2.302 roughly, so calculate the size of them
            reader.Offset = gameObjectListPtr;
            int count = reader.ReadInt32();
            int eachSize;
            if (count > 1)
            {
                int first = reader.ReadInt32();
                eachSize = reader.ReadInt32() - first;
            }
            else
                eachSize = tilePtr - (reader.Offset + 4);
            if (eachSize >= 40)
            {
                reader.VersionInfo.RoomObjectPreCreate = true;
                if (eachSize == 48)
                    reader.VersionInfo.SetVersion(2, 2, 2, 302);
            }
            reader.Offset = gameObjectListPtr;
            GameObjects = new GMUniquePointerList<GameObject>();
            GameObjects.Deserialize(reader);
        }

        public override string ToString()
        {
            return $"Room: \"{Name.Content}\"";
        }

        /// <summary>
        /// Contains information about a background in a room.
        /// </summary>
        public class Background : IGMSerializable
        {
            public bool Enabled;
            public bool Foreground;
            public int BackgroundID;
            public int X, Y;
            public int TileX, TileY;
            public int SpeedX, SpeedY;
            public bool Stretch;

            public void Serialize(GMDataWriter writer)
            {
                writer.WriteWideBoolean(Enabled);
                writer.WriteWideBoolean(Foreground);
                writer.Write(BackgroundID);
                writer.Write(X); writer.Write(Y);
                writer.Write(TileX); writer.Write(TileY);
                writer.Write(SpeedX); writer.Write(SpeedY);
                writer.WriteWideBoolean(Stretch);
            }

            public void Deserialize(GMDataReader reader)
            {
                Enabled = reader.ReadWideBoolean();
                Foreground = reader.ReadWideBoolean();
                BackgroundID = reader.ReadInt32();
                X = reader.ReadInt32(); Y = reader.ReadInt32();
                TileX = reader.ReadInt32(); TileY = reader.ReadInt32();
                SpeedX = reader.ReadInt32(); SpeedY = reader.ReadInt32();
                Stretch = reader.ReadWideBoolean();
            }
        }

        /// <summary>
        /// Contains information about a view in a room.
        /// </summary>
        public class View : IGMSerializable
        {
            public bool Enabled;
            public int ViewX, ViewY, ViewWidth, ViewHeight;
            public int PortX, PortY, PortWidth, PortHeight;
            public int BorderX, BorderY;
            public int SpeedX, SpeedY;
            public int FollowObjectID;

            public void Serialize(GMDataWriter writer)
            {
                writer.WriteWideBoolean(Enabled);
                writer.Write(ViewX); writer.Write(ViewY);
                writer.Write(ViewWidth); writer.Write(ViewHeight);
                writer.Write(PortX); writer.Write(PortY);
                writer.Write(PortWidth); writer.Write(PortHeight);
                writer.Write(BorderX); writer.Write(BorderY);
                writer.Write(SpeedX); writer.Write(SpeedY);
                writer.Write(FollowObjectID);
            }

            public void Deserialize(GMDataReader reader)
            {
                Enabled = reader.ReadWideBoolean();
                ViewX = reader.ReadInt32(); ViewY = reader.ReadInt32();
                ViewWidth = reader.ReadInt32(); ViewHeight = reader.ReadInt32();
                PortX = reader.ReadInt32(); PortY = reader.ReadInt32();
                PortWidth = reader.ReadInt32(); PortHeight = reader.ReadInt32();
                BorderX = reader.ReadInt32(); BorderY = reader.ReadInt32();
                SpeedX = reader.ReadInt32(); SpeedY = reader.ReadInt32();
                FollowObjectID = reader.ReadInt32();
            }
        }

        /// <summary>
        /// Contains information about an object in a room.
        /// </summary>
        public class GameObject : IGMSerializable
        {
            public int X, Y;
            public int ObjectID;
            public int InstanceID;
            public int CreationCodeID;
            public float ScaleX, ScaleY;
            public int Color;
            public float Angle;

            // In some late 1.4 version and above (VersionInfo.RoomObjectPreCreate)
            public int PreCreateCodeID;

            // GMS 2.2.2.302+
            public float ImageSpeed;
            public int ImageIndex;

            public void Serialize(GMDataWriter writer)
            {
                writer.Write(X); writer.Write(Y);
                writer.Write(ObjectID);
                writer.Write(InstanceID);
                writer.Write(CreationCodeID);
                writer.Write(ScaleX); writer.Write(ScaleY);
                if (writer.VersionInfo.IsVersionAtLeast(2, 2, 2, 302))
                {
                    writer.Write(ImageSpeed);
                    writer.Write(ImageIndex);
                }
                writer.Write(Color);
                writer.Write(Angle);
                if (writer.VersionInfo.RoomObjectPreCreate)
                    writer.Write(PreCreateCodeID);
            }

            public void Deserialize(GMDataReader reader)
            {
                X = reader.ReadInt32(); Y = reader.ReadInt32();
                ObjectID = reader.ReadInt32();
                InstanceID = reader.ReadInt32();
                CreationCodeID = reader.ReadInt32();
                ScaleX = reader.ReadSingle(); ScaleY = reader.ReadSingle();
                if (reader.VersionInfo.IsVersionAtLeast(2, 2, 2, 302))
                {
                    ImageSpeed = reader.ReadSingle();
                    ImageIndex = reader.ReadInt32();
                }
                Color = reader.ReadInt32();
                Angle = reader.ReadSingle();
                if (reader.VersionInfo.RoomObjectPreCreate)
                    PreCreateCodeID = reader.ReadInt32();
            }
        }

        /// <summary>
        /// Contains information about a tile in a room.
        /// </summary>
        public class Tile : IGMSerializable
        {
            public int X, Y;
            public int AssetID; // Sprite in GMS2, background before
            public int SourceX, SourceY;
            public int Width, Height;
            public int Depth;
            public int ID;
            public float ScaleX, ScaleY;
            public int Color;

            public void Serialize(GMDataWriter writer)
            {
                writer.Write(X); writer.Write(Y);
                writer.Write(AssetID);
                writer.Write(SourceX); writer.Write(SourceY);
                writer.Write(Width); writer.Write(Height);
                writer.Write(Depth);
                writer.Write(ID);
                writer.Write(ScaleX); writer.Write(ScaleY);
                writer.Write(Color);
            }

            public void Deserialize(GMDataReader reader)
            {
                X = reader.ReadInt32(); Y = reader.ReadInt32();
                AssetID = reader.ReadInt32();
                SourceX = reader.ReadInt32(); SourceY = reader.ReadInt32();
                Width = reader.ReadInt32(); Height = reader.ReadInt32();
                Depth = reader.ReadInt32();
                ID = reader.ReadInt32();
                ScaleX = reader.ReadSingle(); ScaleY = reader.ReadSingle();
                Color = reader.ReadInt32();
            }
        }

        /// <summary>
        /// Contains information about a layer in a room.
        /// </summary>
        public class Layer : IGMSerializable
        {
            public enum LayerKind : int
            {
                Background = 1,
                Instances = 2,
                Assets = 3,
                Tiles = 4,
                Effect = 6,
            }

            public GMString Name;
            public int ID;
            public LayerKind Kind;
            public int Depth;
            public float OffsetX, OffsetY;
            public float HSpeed, VSpeed;
            public bool Visible;

            public bool EffectEnabled;
            public GMString EffectType;
            public GMList<EffectProperty> EffectProperties;

            // Only one of these aren't null at a time
            public LayerBackground Background;
            public LayerInstances Instances;
            public LayerAssets Assets;
            public LayerTiles Tiles;
            public LayerEffect Effect;

            public void Serialize(GMDataWriter writer)
            {
                writer.WritePointerString(Name);
                writer.Write(ID);
                writer.Write((int)Kind);
                writer.Write(Depth);
                writer.Write(OffsetX); writer.Write(OffsetY);
                writer.Write(HSpeed); writer.Write(VSpeed);
                writer.WriteWideBoolean(Visible);

                if (writer.VersionInfo.Major >= 2022)
                {
                    writer.WriteWideBoolean(EffectEnabled);
                    writer.WritePointerString(EffectType);
                    EffectProperties.Serialize(writer);
                }

                switch (Kind)
                {
                    case LayerKind.Background:
                        Background.Serialize(writer);
                        break;
                    case LayerKind.Instances:
                        Instances.Serialize(writer);
                        break;
                    case LayerKind.Assets:
                        Assets.Serialize(writer);
                        break;
                    case LayerKind.Tiles:
                        Tiles.Serialize(writer);
                        break;
                    case LayerKind.Effect:
                        Effect.Serialize(writer);
                        break;
                    default:
                        writer.Warnings.Add(new GMWarning($"Unknown layer kind {Kind}"));
                        break;
                }
            }

            public void Deserialize(GMDataReader reader)
            {
                Name = reader.ReadStringPointerObject();
                ID = reader.ReadInt32();
                Kind = (LayerKind)reader.ReadInt32();
                Depth = reader.ReadInt32();
                OffsetX = reader.ReadSingle(); OffsetY = reader.ReadSingle();
                HSpeed = reader.ReadSingle(); VSpeed = reader.ReadSingle();
                Visible = reader.ReadWideBoolean();

                if (reader.VersionInfo.Major >= 2022)
                {
                    EffectEnabled = reader.ReadWideBoolean();
                    EffectType = reader.ReadStringPointerObject();
                    EffectProperties = new GMList<EffectProperty>();
                    EffectProperties.Deserialize(reader);
                }

                switch (Kind)
                {
                    case LayerKind.Background:
                        Background = new LayerBackground();
                        Background.Deserialize(reader);
                        break;
                    case LayerKind.Instances:
                        Instances = new LayerInstances();
                        Instances.Deserialize(reader);
                        break;
                    case LayerKind.Assets:
                        Assets = new LayerAssets();
                        Assets.Deserialize(reader);
                        break;
                    case LayerKind.Tiles:
                        Tiles = new LayerTiles();
                        Tiles.Deserialize(reader);
                        break;
                    case LayerKind.Effect:
                        Effect = new LayerEffect();
                        Effect.Deserialize(reader);
                        break;
                    default:
                        reader.Warnings.Add(new GMWarning($"Unknown layer kind {Kind}"));
                        break;
                }
            }

            public override string ToString()
            {
                return Name.Content;
            }

            public class LayerBackground : IGMSerializable
            {
                public bool Visible;
                public bool Foreground;
                public int SpriteID;
                public bool TileHorz, TileVert;
                public bool Stretch;
                public int Color;
                public float FirstFrame;
                public float AnimationSpeed;
                public GMSprite.AnimSpeedType AnimationSpeedType;

                public void Serialize(GMDataWriter writer)
                {
                    writer.WriteWideBoolean(Visible);
                    writer.WriteWideBoolean(Foreground);
                    writer.Write(SpriteID);
                    writer.WriteWideBoolean(TileHorz);
                    writer.WriteWideBoolean(TileVert);
                    writer.WriteWideBoolean(Stretch);
                    writer.Write(Color);
                    writer.Write(FirstFrame);
                    writer.Write(AnimationSpeed);
                    writer.Write((int)AnimationSpeedType);
                }

                public void Deserialize(GMDataReader reader)
                {
                    Visible = reader.ReadWideBoolean();
                    Foreground = reader.ReadWideBoolean();
                    SpriteID = reader.ReadInt32();
                    TileHorz = reader.ReadWideBoolean();
                    TileVert = reader.ReadWideBoolean();
                    Stretch = reader.ReadWideBoolean();
                    Color = reader.ReadInt32();
                    FirstFrame = reader.ReadSingle();
                    AnimationSpeed = reader.ReadSingle();
                    AnimationSpeedType = (GMSprite.AnimSpeedType)reader.ReadInt32();
                }
            }

            public class LayerInstances : IGMSerializable
            {
                // IDs corresponding to the IDs given in the GameObjects list in the room
                public List<int> Instances { get; set; }

                public void Serialize(GMDataWriter writer)
                {
                    writer.Write(Instances.Count);
                    foreach (int i in Instances)
                        writer.Write(i);
                }

                public void Deserialize(GMDataReader reader)
                {
                    int count = reader.ReadInt32();
                    Instances = new List<int>(count);
                    for (int i = count; i > 0; i--)
                        Instances.Add(reader.ReadInt32());
                }
            }

            public class LayerAssets : IGMSerializable
            {
                public GMUniquePointerList<Tile> LegacyTiles;
                public GMUniquePointerList<AssetInstance> Sprites;

                // GMS 2.3+
                public GMUniquePointerList<AssetInstance> Sequences;
                public GMUniquePointerList<AssetInstance> NineSlices; // apparently removed in 2.3.2

                public void Serialize(GMDataWriter writer)
                {
                    writer.WritePointer(LegacyTiles);
                    writer.WritePointer(Sprites);
                    if (writer.VersionInfo.IsVersionAtLeast(2, 3))
                    {
                        writer.WritePointer(Sequences);
                        if (!writer.VersionInfo.IsVersionAtLeast(2, 3, 2))
                            writer.WritePointer(NineSlices);
                    }

                    writer.WriteObjectPointer(LegacyTiles);
                    LegacyTiles.Serialize(writer);
                    writer.WriteObjectPointer(Sprites);
                    Sprites.Serialize(writer);
                    if (writer.VersionInfo.IsVersionAtLeast(2, 3))
                    {
                        writer.WriteObjectPointer(Sequences);
                        Sequences.Serialize(writer);
                        if (!writer.VersionInfo.IsVersionAtLeast(2, 3, 2))
                        {
                            if (NineSlices == null)
                                writer.Write(0); // Even if it's 2.3.2 but we don't detect it, this shouldn't break format... probably
                            else
                            {
                                writer.WriteObjectPointer(NineSlices);
                                NineSlices.Serialize(writer);
                            }
                        }
                    }
                }

                public void Deserialize(GMDataReader reader)
                {
                    LegacyTiles = reader.ReadPointerObjectUnique<GMUniquePointerList<Tile>>();
                    Sprites = reader.ReadPointerObjectUnique<GMUniquePointerList<AssetInstance>>();

                    if (reader.VersionInfo.IsVersionAtLeast(2, 3))
                    {
                        Sequences = reader.ReadPointerObjectUnique<GMUniquePointerList<AssetInstance>>();
                        if (!reader.VersionInfo.IsVersionAtLeast(2, 3, 2))
                            NineSlices = reader.ReadPointerObjectUnique<GMUniquePointerList<AssetInstance>>();
                    }
                }
            }

            public class AssetInstance : IGMSerializable
            {
                public GMString Name;
                public int AssetID;
                public int X, Y;
                public float ScaleX, ScaleY;
                public int Color;
                public float AnimationSpeed;
                public GMSprite.AnimSpeedType AnimationSpeedType;
                public float FrameIndex;
                public float Rotation;

                public void Serialize(GMDataWriter writer)
                {
                    writer.WritePointerString(Name);
                    writer.Write(AssetID);
                    writer.Write(X); writer.Write(Y);
                    writer.Write(ScaleX); writer.Write(ScaleY);
                    writer.Write(Color);
                    writer.Write(AnimationSpeed);
                    writer.Write((int)AnimationSpeedType);
                    writer.Write(FrameIndex);
                    writer.Write(Rotation);
                }

                public void Deserialize(GMDataReader reader)
                {
                    Name = reader.ReadStringPointerObject();
                    AssetID = reader.ReadInt32();
                    X = reader.ReadInt32(); Y = reader.ReadInt32();
                    ScaleX = reader.ReadSingle(); ScaleY = reader.ReadSingle();
                    Color = reader.ReadInt32();
                    AnimationSpeed = reader.ReadSingle();
                    AnimationSpeedType = (GMSprite.AnimSpeedType)reader.ReadInt32();
                    FrameIndex = reader.ReadSingle();
                    Rotation = reader.ReadSingle();
                }

                public override string ToString()
                {
                    return Name.Content;
                }
            }

            public class LayerTiles : IGMSerializable
            {
                public int BackgroundID;
                public int TilesX, TilesY;
                public int[][] TileData;

                public void Serialize(GMDataWriter writer)
                {
                    writer.Write(BackgroundID);
                    writer.Write(TilesX);
                    writer.Write(TilesY);
                    for (int y = 0; y < TilesY; y++)
                    {
                        for (int x = 0; x < TilesX; x++)
                            writer.Write(TileData[y][x]);
                    }
                }

                public void Deserialize(GMDataReader reader)
                {
                    BackgroundID = reader.ReadInt32();
                    TilesX = reader.ReadInt32();
                    TilesY = reader.ReadInt32();
                    TileData = new int[TilesY][];
                    for (int y = 0; y < TilesY; y++)
                    {
                        TileData[y] = new int[TilesX];
                        for (int x = 0; x < TilesX; x++)
                            TileData[y][x] = reader.ReadInt32();
                    }
                }
            }

            public class LayerEffect : IGMSerializable
            {
                public GMString EffectType;
                public GMList<EffectProperty> Properties;

                public void Serialize(GMDataWriter writer)
                {
                    writer.WritePointerString(EffectType);
                    Properties.Serialize(writer);
                }

                public void Deserialize(GMDataReader reader)
                {
                    EffectType = reader.ReadStringPointerObject();
                    Properties = new GMList<EffectProperty>();
                    Properties.Deserialize(reader);
                }
            }

            public class EffectProperty : IGMSerializable
            {
                public enum PropertyType
                {
                    Real = 0,
                    Color = 1,
                    Sampler = 2
                }

                public PropertyType Kind;
                public GMString Name;
                public GMString Value;

                public void Serialize(GMDataWriter writer)
                {
                    writer.Write((int)Kind);
                    writer.WritePointerString(Name);
                    writer.WritePointerString(Value);
                }

                public void Deserialize(GMDataReader reader)
                {
                    Kind = (PropertyType)reader.ReadInt32();
                    Name = reader.ReadStringPointerObject();
                    Value = reader.ReadStringPointerObject();
                }
            }
        }
    }
}
