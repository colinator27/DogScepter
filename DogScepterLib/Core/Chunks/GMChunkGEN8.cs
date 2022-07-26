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
            None = 0x0,
            Internet = 0x1,
            Joystick = 0x2,
            Gamepad = 0x4,
            ReadScreenPixels = 0x10,
            Math = 0x20,
            Action = 0x40,
            D3D_State = 0x80,
            D3D_Primitive = 0x100,
            DataStructure = 0x200,
            File_Legacy = 0x400,
            Ini = 0x800,
            Filename = 0x1000,
            Directory = 0x2000,
            Shell = 0x4000,
            Obsolete = 0x8000,
            Http = 0x10000,
            JsonZip = 0x20000,
            Debug = 0x40000,
            Motion = 0x80000,
            Collision = 0x100000,
            Instance = 0x200000,
            Room = 0x400000,
            Game = 0x800000,
            Display = 0x1000000,
            Device = 0x2000000,
            Window = 0x4000000,
            Draw = 0x8000000,
            Texture = 0x10000000,
            Graphics = 0x20000000,
            String = 0x40000000,
            Tile = 0x80000000,
            Surface = 0x100000000,
            Skeleton = 0x200000000,
            IO = 0x400000000,
            GMSystem = 0x800000000,
            Array = 0x1000000000,
            External = 0x2000000000,
            Push = 0x4000000000,
            Date = 0x8000000000,
            Particle = 0x10000000000,
            Resource = 0x20000000000,
            Html5 = 0x40000000000,
            Sound = 0x80000000000,
            Audio = 0x100000000000,
            Event = 0x200000000000,
            Script = 0x400000000000,
            Text = 0x800000000000,
            Analytics = 0x1000000000000,
            Object = 0x2000000000000,
            Asset = 0x4000000000000,
            Achievement = 0x8000000000000,
            Cloud = 0x10000000000000,
            Ads = 0x20000000000000,
            Os = 0x40000000000000,
            iap = 0x80000000000000,
            Facebook = 0x100000000000000,
            Physics = 0x200000000000000,
            SWF = 0x400000000000000,
            PlatformSpecific = 0x800000000000000,
            Buffer = 0x1000000000000000,
            Steam = 0x2000000000000000,
            Steam_UGC = 0x2010000000000000,
            Shader = 0x4000000000000000,
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
        public BufferRegion LicenseMD5;
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
            writer.VersionInfo.SetVersion(Major, Minor, Release, Build);
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
                long infoNumber = GetInfoNumber(firstRandom, writer.VersionInfo.RunFromIDE);
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

        public override void Deserialize(GMDataReader reader)
        {
            base.Deserialize(reader);

            DisableDebug = reader.ReadBoolean();
            FormatID = reader.ReadByte();
            reader.VersionInfo.FormatID = FormatID;
            Unknown = reader.ReadInt16();
            Filename = reader.ReadStringPointerObject();
            Config = reader.ReadStringPointerObject();
            LastObjectID = reader.ReadInt32();
            LastTileID = reader.ReadInt32();
            GameID = reader.ReadInt32();
            LegacyGUID = new Guid(reader.ReadBytes(16).Memory.ToArray());
            GameName = reader.ReadStringPointerObject();
            Major = reader.ReadInt32();
            Minor = reader.ReadInt32();
            Release = reader.ReadInt32();
            Build = reader.ReadInt32();
            reader.VersionInfo.SetVersion(Major, Minor, Release, Build);
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
                int infoLocation = Math.Abs((int)(Timestamp & 65535L) / 7 + (GameID - DefaultWindowWidth) + RoomOrder.Count) % 4;
                for (int i = 0; i < 4; i++)
                {
                    if (i == infoLocation)
                    {
                        long curr = reader.ReadInt64();
                        GMS2_RandomUID.Add(curr);
                        if (curr != GetInfoNumber(firstRandom, false))
                        {
                            if (curr != GetInfoNumber(firstRandom, true))
                                reader.Warnings.Add(new GMWarning("Unexpected random UID info", GMWarning.WarningLevel.Info));
                            else
                                reader.VersionInfo.RunFromIDE = true;
                        }
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
                GMS2_GameGUID = new Guid(reader.ReadBytes(16).Memory.ToArray());
            }
        }

        private long GetInfoNumber(long firstRandom, bool runFromIDE)
        {
            long infoNumber = Timestamp;
            if (!runFromIDE)
                infoNumber -= 1000;
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
            return infoNumber;
        }
    }
}
