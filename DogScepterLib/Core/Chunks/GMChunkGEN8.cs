using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Chunks
{
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

        public bool DisableDebug;
        public byte FormatID;
        public short Unknown;
        public GMString Filename;
        public GMString Config;
        public int LastObjectID;
        public int LastTileID;
        public int GameID;
        public int[] UnknownZeros;
        public GMString GameName;
        public int Major, Minor, Release, Build;
        public int DefaultWindowWidth, DefaultWindowHeight;
        public InfoFlags Info;
        public byte[] LicenseMD5;
        public int LicenseCRC32;
        public long Timestamp;
        public GMString DisplayName;
        public int ActiveTargets1, ActiveTargets2;
        public int FunctionClassifications1, FunctionClassifications2;
        public int SteamAppID;
        public int DebuggerPort;

        public override void Serialize(GMDataWriter writer)
        {
            base.Serialize(writer);

            writer.WritePointerString(Filename);
            // TODO: everything else, this is just a STRG test at the moment
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
            UnknownZeros = new int[4];
            for (int i = 0; i < 4; i++)
            {
                UnknownZeros[i] = reader.ReadInt32();
                if (UnknownZeros[i] != 0)
                    reader.Warnings.Add(new GMWarning("An unknown 0 wasn't 0!", GMWarning.WarningLevel.Info));
            }
            GameName = reader.ReadStringPointerObject();
            Major = reader.ReadInt32();
            Minor = reader.ReadInt32();
            Release = reader.ReadInt32();
            Build = reader.ReadInt32();
            reader.VersionInfo.SetNumber(Major, Minor, Release, Build);
            DefaultWindowWidth = reader.ReadInt32();
            DefaultWindowHeight = reader.ReadInt32();
            Info = (InfoFlags)reader.ReadInt32();
            LicenseMD5 = reader.ReadBytes(16);
            LicenseCRC32 = reader.ReadInt32();
            Timestamp = reader.ReadInt64();
            DisplayName = reader.ReadStringPointerObject();

            // TODO: Parse these flags properly! This may be important for initializing systems in GameMaker
            ActiveTargets1 = reader.ReadInt32();
            ActiveTargets2 = reader.ReadInt32();
            FunctionClassifications1 = reader.ReadInt32();
            FunctionClassifications2 = reader.ReadInt32();

            SteamAppID = reader.ReadInt32();
            DebuggerPort = reader.ReadInt32();

            // TODO read the rest
        }
    }
}
