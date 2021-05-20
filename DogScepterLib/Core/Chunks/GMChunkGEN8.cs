using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Chunks
{
    /// <summary>
    /// Contains metadata about the GameMaker game.
    /// </summary>
    public class GMChunkGEN8 : GMChunk
    {
        [Flags]
        public enum InfoFlags : uint
        {
            Fullscreen = 0x0001,        // Start fullscreen
            SyncVertex1 = 0x0002,       // Use synchronization to avoid tearing
            SyncVertex2 = 0x0004,
            Interpolate = 0x0008,       // Interpolate colours between pixels
            Scale = 0x0010,             // Scaling: Keep aspect
            ShowCursor = 0x0020,        // Display cursor
            Sizeable = 0x0040,          // Allow window resize
            ScreenKey = 0x0080,         // Allow fullscreen switching
            SyncVertex3 = 0x0100,
            StudioVersionB1 = 0x0200,
            StudioVersionB2 = 0x0400,
            StudioVersionB3 = 0x0800,
            StudioVersionMask = 0x0E00, // studioVersion = (infoFlags & InfoFlags.StudioVersionMask) >> 9
            SteamOrPlayer = 0x1000,     // Steam or YoYo Player
            LocalDataEnabled = 0x2000,
            BorderlessWindow = 0x4000,  // Borderless Window
            DefaultCodeKind = 0x8000,
            LicenseExclusions = 0x10000,
        }

        [Flags]
        public enum FunctionClassification : ulong
        {
            Network = 0x1,
            Joystick = 0x2, // Maybe not used?
            Gamepad = 0x4,
            Screen = 0x10,
            Math = 0x20,
            LegacyAction = 0x40,
            GPU = 0x80,
            D3DModel = 0x100,
            DataStructures = 0x200,
            File = 0x400,
            INI = 0x800,
            Filename = 0x1000,
            Directory = 0x2000,
            Environment = 0x4000,
            Unknown1 = 0x8000,
            HTTP = 0x10000,
            Encoding = 0x20000,
            Dialog = 0x40000,
            MotionPlanning = 0x80000,
            ShapeCollision = 0x100000,
            Instance = 0x200000,
            Room = 0x400000,
            Game = 0x800000,
            Display = 0x1000000,
            Device = 0x2000000,
            Window = 0x4000000,
            DrawBasic = 0x8000000,
            Texture = 0x10000000,
            Layer = 0x20000000,
            String = 0x40000000,
            LegacyTiles = 0x80000000,
            Surface = 0x100000000,
            Spine = 0x200000000,
            Input = 0x400000000,
            DataTypes = 0x800000000,
            Array = 0x1000000000,
            ExternalCall = 0x2000000000,
            Notification = 0x4000000000,
            Date = 0x8000000000,
            Particle = 0x10000000000,
            Sprite = 0x20000000000,
            Clickable = 0x40000000000,
            LegacySound = 0x80000000000,
            Audio = 0x100000000000,
            Event = 0x200000000000,
            Unknown2 = 0x400000000000,
            Font = 0x800000000000,
            Analytics = 0x1000000000000,
            Unknown3 = 0x2000000000000,
            Unknown4 = 0x4000000000000,
            Achievement = 0x8000000000000,
            CloudSaving = 0x10000000000000,
            Ads = 0x20000000000000,
            OS = 0x40000000000000,
            IAP = 0x80000000000000,
            LegacyFacebook = 0x100000000000000,
            Physics = 0x200000000000000,
            SWFAA = 0x400000000000000,
            Console = 0x800000000000000,
            Buffer = 0x1000000000000000,
            Steam = 0x2000000000000000,
            Shaders = 0x4000000000000000,
            Vertex = 0x8000000000000000
        }

        public bool DisableDebug;
        public byte FormatID;
        public short Unknown;
        public GMString Filename;
        public GMString Config;
        public int LastObjectID;
        public int LastTileID;
        public int GameID;
        public Guid LegacyGUID;
        public GMString GameName;
        public int Major, Minor, Release, Build;
        public int DefaultWindowWidth, DefaultWindowHeight;
        public InfoFlags Info;
        public byte[] LicenseMD5;
        public int LicenseCRC32;
        public long Timestamp;
        public GMString DisplayName;
        public long ActiveTargets;
        public FunctionClassification FunctionClassifications;
        public int SteamAppID;
        public int DebuggerPort;
        public List<int> RoomOrder;

        public List<long> GMS2_RandomUID;
        public float GMS2_FPS;
        public bool GMS2_AllowStatistics;
        public Guid GMS2_GameGUID;

        public override void Serialize(GMDataWriter writer)
        {
            base.Serialize(writer);

            writer.Write(DisableDebug);
            writer.Write(FormatID);
            writer.VersionInfo.FormatID = FormatID;
            writer.Write(Unknown);
            writer.WritePointerString(Filename);
            writer.WritePointerString(Config);
            writer.Write(LastObjectID);
            writer.Write(LastTileID);
            writer.Write(GameID);
            writer.Write(LegacyGUID.ToByteArray());
            writer.WritePointerString(GameName);
            writer.Write(Major);
            writer.Write(Minor);
            writer.Write(Release);
            writer.Write(Build);
            writer.VersionInfo.SetNumber(Major, Minor, Release, Build);
            writer.Write(DefaultWindowWidth);
            writer.Write(DefaultWindowHeight);
            writer.Write((uint)Info);
            writer.Write(LicenseCRC32);
            writer.Write(LicenseMD5);
            writer.Write(Timestamp);
            writer.WritePointerString(DisplayName);
            writer.Write(ActiveTargets);
            writer.Write((ulong)FunctionClassifications);
            writer.Write(SteamAppID);
            if (FormatID >= 14)
                writer.Write(DebuggerPort);

            writer.Write(RoomOrder.Count);
            for (int i = 0; i < RoomOrder.Count; i++)
                writer.Write(RoomOrder[i]);

            if (writer.VersionInfo.Major >= 2)
            {
                // Write random UID
                Random random = new Random((int)(Timestamp & 4294967295L));
                long firstRandom = (long)random.Next() << 32 | (long)random.Next();
                long infoNumber = Timestamp - 1000;
                ulong temp = (ulong)infoNumber;
                temp = ((temp << 56 & 18374686479671623680UL) | (temp >> 8 & 71776119061217280UL) |
                        (temp << 32 & 280375465082880UL) | (temp >> 16 & 1095216660480UL) | (temp << 8 & 4278190080UL) |
                        (temp >> 24 & 16711680UL) | (temp >> 16 & 65280UL) | (temp >> 32 & 255UL));
                infoNumber = (long)temp;
                infoNumber ^= firstRandom;
                infoNumber = ~infoNumber;
                infoNumber ^= ((long)GameID << 32 | (long)GameID);
                infoNumber ^= ((long)(DefaultWindowWidth + (int)Info) << 48 |
                               (long)(DefaultWindowHeight + (int)Info) << 32 |
                               (long)(DefaultWindowHeight + (int)Info) << 16 |
                               (long)(DefaultWindowWidth + (int)Info));
                infoNumber ^= FormatID;
                int infoLocation = Math.Abs((int)(Timestamp & 65535L) / 7 + (GameID - DefaultWindowWidth) + RoomOrder.Count) % 4;
                GMS2_RandomUID.Clear();
                writer.Write(firstRandom);
                GMS2_RandomUID.Add(firstRandom);
                for (int i = 0; i < 4; i++)
                {
                    if (i == infoLocation)
                    {
                        writer.Write(infoNumber);
                        GMS2_RandomUID.Add(infoNumber);
                    }
                    else
                    {
                        int first = random.Next();
                        int second = random.Next();
                        writer.Write(first);
                        writer.Write(second);
                        GMS2_RandomUID.Add(((long)first << 32) | (long)second);
                    }
                }

                // Other GMS2-specific data
                writer.Write(GMS2_FPS);
                writer.WriteWideBoolean(GMS2_AllowStatistics);
                writer.Write(GMS2_GameGUID.ToByteArray());
            }
        }

        public override void Unserialize(GMDataReader reader)
        {
            base.Unserialize(reader);

            DisableDebug = reader.ReadBoolean();
            FormatID = reader.ReadByte();
            reader.VersionInfo.FormatID = FormatID;
            Unknown = reader.ReadInt16();
            Filename = reader.ReadStringPointerObject();
            Config = reader.ReadStringPointerObject();
            LastObjectID = reader.ReadInt32();
            LastTileID = reader.ReadInt32();
            GameID = reader.ReadInt32();
            LegacyGUID = new Guid(reader.ReadBytes(16));
            GameName = reader.ReadStringPointerObject();
            Major = reader.ReadInt32();
            Minor = reader.ReadInt32();
            Release = reader.ReadInt32();
            Build = reader.ReadInt32();
            reader.VersionInfo.SetNumber(Major, Minor, Release, Build);
            DefaultWindowWidth = reader.ReadInt32();
            DefaultWindowHeight = reader.ReadInt32();
            Info = (InfoFlags)reader.ReadUInt32();
            LicenseCRC32 = reader.ReadInt32();
            LicenseMD5 = reader.ReadBytes(16);
            Timestamp = reader.ReadInt64();
            DisplayName = reader.ReadStringPointerObject();
            ActiveTargets = reader.ReadInt64();
            FunctionClassifications = (FunctionClassification)reader.ReadUInt64();
            SteamAppID = reader.ReadInt32();
            if (FormatID >= 14)
                DebuggerPort = reader.ReadInt32();

            int count = reader.ReadInt32();
            RoomOrder = new List<int>(count);
            for (int i = 0; i < count; i++)
                RoomOrder.Add(reader.ReadInt32());

            if (reader.VersionInfo.Major >= 2)
            {
                // Begin parsing random UID, and verify it based on original algorithm
                GMS2_RandomUID = new List<long>();

                Random random = new Random((int)(Timestamp & 4294967295L));
                long firstRandom = (long)random.Next() << 32 | (long)random.Next();
                if (reader.ReadInt64() != firstRandom)
                    reader.Warnings.Add(new GMWarning("Unexpected random UID", GMWarning.WarningLevel.Info));
                long infoNumber = Timestamp - 1000;
                ulong temp = (ulong)infoNumber;
                temp = ((temp << 56 & 18374686479671623680UL) | (temp >> 8 & 71776119061217280UL) | 
                        (temp << 32 & 280375465082880UL) | (temp >> 16 & 1095216660480UL) | (temp << 8 & 4278190080UL) | 
                        (temp >> 24 & 16711680UL) | (temp >> 16 & 65280UL) | (temp >> 32 & 255UL));
                infoNumber = (long)temp;
                infoNumber ^= firstRandom;
                infoNumber = ~infoNumber;
                infoNumber ^= ((long)GameID << 32 | (long)GameID);
                infoNumber ^= ((long)(DefaultWindowWidth + (int)Info) << 48 | 
                               (long)(DefaultWindowHeight + (int)Info) << 32 | 
                               (long)(DefaultWindowHeight + (int)Info) << 16 | 
                               (long)(DefaultWindowWidth + (int)Info));
                infoNumber ^= FormatID;
                int infoLocation = Math.Abs((int)(Timestamp & 65535L) / 7 + (GameID - DefaultWindowWidth) + RoomOrder.Count) % 4;
                for (int i = 0; i < 4; i++)
                {
                    if (i == infoLocation)
                    {
                        long curr = reader.ReadInt64();
                        GMS2_RandomUID.Add(curr);
                        if (curr != infoNumber)
                            reader.Warnings.Add(new GMWarning("Unexpected random UID info", GMWarning.WarningLevel.Info));
                    }
                    else
                    {
                        int first = reader.ReadInt32();
                        int second = reader.ReadInt32();
                        if (first != random.Next())
                            reader.Warnings.Add(new GMWarning("Unexpected random UID", GMWarning.WarningLevel.Info));
                        if (second != random.Next())
                            reader.Warnings.Add(new GMWarning("Unexpected random UID", GMWarning.WarningLevel.Info));
                        GMS2_RandomUID.Add((long)(first << 32) | (long)second);
                    }
                }

                // Other GMS2-specific data
                GMS2_FPS = reader.ReadSingle();
                GMS2_AllowStatistics = reader.ReadWideBoolean();
                GMS2_GameGUID = new Guid(reader.ReadBytes(16));
            }
        }
    }
}
