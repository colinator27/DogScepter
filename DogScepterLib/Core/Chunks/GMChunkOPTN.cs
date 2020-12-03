using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace DogScepterLib.Core.Chunks
{
    public class GMChunkOPTN : GMChunk
    {
        [Flags]
        public enum OptionsFlags : ulong
        {
            FullScreen = 0x1,
            InterpolatePixels = 0x2,
            UseNewAudio = 0x4,
            NoBorder = 0x8,
            ShowCursor = 0x10,
            Sizeable = 0x20,
            StayOnTop = 0x40,
            ChangeResolution = 0x80,
            NoButtons = 0x100,
            ScreenKey = 0x200,
            HelpKey = 0x400,
            QuitKey = 0x800,
            SaveKey = 0x1000,
            ScreenShotKey = 0x2000,
            CloseSec = 0x4000,
            Freeze = 0x8000,
            ShowProgress = 0x10000,
            LoadTransparent = 0x20000,
            ScaleProgress = 0x40000,
            DisplayErrors = 0x80000,
            WriteErrors = 0x100000,
            AbortErrors = 0x200000,
            VariableErrors = 0x400000,
            CreationEventOrder = 0x800000,
            UseFrontTouch = 0x1000000,
            UseRearTouch = 0x2000000,
            UseFastCollision = 0x4000000,
            FastCollisionCompatibility = 0x8000000,
            DisableSandbox = 0x10000000
        }

        public ulong Unknown;
        public OptionsFlags Options;
        public uint Scale;
        public uint WindowColor;
        public uint ColorDepth;
        public uint Resolution;
        public uint Frequency;
        public uint VertexSync;
        public uint Priority;

        // Seemingly unused splash textures? Not sure if supplying these does anything
        public GMTextureItem SplashBackImage;
        public GMTextureItem SplashFrontImage;
        public GMTextureItem SplashLoadImage;
        public uint LoadAlpha;

        public GMList<Constant> Constants;

        public override void Serialize(GMDataWriter writer)
        {
            base.Serialize(writer);

            writer.Write(Unknown);
            writer.Write((ulong)Options);
            writer.Write(Scale);
            writer.Write(WindowColor);
            writer.Write(ColorDepth);
            writer.Write(Resolution);
            writer.Write(Frequency);
            writer.Write(VertexSync);
            writer.Write(Priority);
            writer.WritePointer(SplashBackImage);
            writer.WritePointer(SplashFrontImage);
            writer.WritePointer(SplashLoadImage);
            writer.Write(LoadAlpha);
            Constants.Serialize(writer);
        }

        public override void Unserialize(GMDataReader reader)
        {
            base.Unserialize(reader);

            Unknown = reader.ReadUInt64();
            Options = (OptionsFlags)reader.ReadUInt64();
            Scale = reader.ReadUInt32();
            WindowColor = reader.ReadUInt32();
            ColorDepth = reader.ReadUInt32();
            Resolution = reader.ReadUInt32();
            Frequency = reader.ReadUInt32();
            VertexSync = reader.ReadUInt32();
            Priority = reader.ReadUInt32();
            SplashBackImage = reader.ReadPointerObject<GMTextureItem>();
            SplashFrontImage = reader.ReadPointerObject<GMTextureItem>();
            SplashLoadImage = reader.ReadPointerObject<GMTextureItem>();
            LoadAlpha = reader.ReadUInt32();
            Constants = new GMList<Constant>();
            Constants.Unserialize(reader);
        }

        public class Constant : GMSerializable
        {
            public GMString Name;
            public GMString Value;

            public void Serialize(GMDataWriter writer)
            {
                writer.WritePointerString(Name);
                writer.WritePointerString(Value);
            }

            public void Unserialize(GMDataReader reader)
            {
                Name = reader.ReadStringPointerObject();
                Value = reader.ReadStringPointerObject();
            }
        }
    }
}
