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
            DisableSandbox = 0x10000000,
            CopyOnWriteEnabled = 0x20000000
        }

        public ulong Unknown;
        public OptionsFlags Options;
        public int Scale;
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

        private void WriteOption(GMDataWriter writer, OptionsFlags flag)
        {
            writer.WriteWideBoolean((Options & flag) == flag);
        }

        public override void Serialize(GMDataWriter writer)
        {
            base.Serialize(writer);

            if (writer.VersionInfo.OptionBitflag)
            {
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
            }
            else
            {
                WriteOption(writer, OptionsFlags.FullScreen);
                WriteOption(writer, OptionsFlags.InterpolatePixels);
                WriteOption(writer, OptionsFlags.UseNewAudio);
                WriteOption(writer, OptionsFlags.NoBorder);
                WriteOption(writer, OptionsFlags.ShowCursor);
                writer.Write(Scale);
                WriteOption(writer, OptionsFlags.Sizeable);
                WriteOption(writer, OptionsFlags.StayOnTop);
                writer.Write(WindowColor);
                WriteOption(writer, OptionsFlags.ChangeResolution);
                writer.Write(ColorDepth);
                writer.Write(Resolution);
                writer.Write(Frequency);
                WriteOption(writer, OptionsFlags.NoButtons);
                writer.Write(VertexSync);
                WriteOption(writer, OptionsFlags.ScreenKey);
                WriteOption(writer, OptionsFlags.HelpKey);
                WriteOption(writer, OptionsFlags.QuitKey);
                WriteOption(writer, OptionsFlags.SaveKey);
                WriteOption(writer, OptionsFlags.ScreenShotKey);
                WriteOption(writer, OptionsFlags.CloseSec);
                writer.Write(Priority);
                WriteOption(writer, OptionsFlags.Freeze);
                WriteOption(writer, OptionsFlags.ShowProgress);
                writer.WritePointer(SplashBackImage);
                writer.WritePointer(SplashFrontImage);
                writer.WritePointer(SplashLoadImage);
                WriteOption(writer, OptionsFlags.LoadTransparent);
                writer.Write(LoadAlpha);
                WriteOption(writer, OptionsFlags.ScaleProgress);
                WriteOption(writer, OptionsFlags.DisplayErrors);
                WriteOption(writer, OptionsFlags.WriteErrors);
                WriteOption(writer, OptionsFlags.AbortErrors);
                WriteOption(writer, OptionsFlags.VariableErrors);
                WriteOption(writer, OptionsFlags.CreationEventOrder);
            }
            Constants.Serialize(writer);
        }

        private void ReadOption(GMDataReader reader, OptionsFlags flag)
        {
            if (reader.ReadWideBoolean())
                Options |= flag;
        }

        public override void Deserialize(GMDataReader reader)
        {
            base.Deserialize(reader);

            reader.VersionInfo.OptionBitflag = (reader.ReadInt32() == int.MinValue);
            reader.Offset -= 4;

            if (reader.VersionInfo.OptionBitflag)
            {
                Unknown = reader.ReadUInt64();
                Options = (OptionsFlags)reader.ReadUInt64();
                Scale = reader.ReadInt32();
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
            }
            else
            {
                Options = 0;
                ReadOption(reader, OptionsFlags.FullScreen);
                ReadOption(reader, OptionsFlags.InterpolatePixels);
                ReadOption(reader, OptionsFlags.UseNewAudio);
                ReadOption(reader, OptionsFlags.NoBorder);
                ReadOption(reader, OptionsFlags.ShowCursor);
                Scale = reader.ReadInt32();
                ReadOption(reader, OptionsFlags.Sizeable);
                ReadOption(reader, OptionsFlags.StayOnTop);
                WindowColor = reader.ReadUInt32();
                ReadOption(reader, OptionsFlags.ChangeResolution);
                ColorDepth = reader.ReadUInt32();
                Resolution = reader.ReadUInt32();
                Frequency = reader.ReadUInt32();
                ReadOption(reader, OptionsFlags.NoButtons);
                VertexSync = reader.ReadUInt32();
                ReadOption(reader, OptionsFlags.ScreenKey);
                ReadOption(reader, OptionsFlags.HelpKey);
                ReadOption(reader, OptionsFlags.QuitKey);
                ReadOption(reader, OptionsFlags.SaveKey);
                ReadOption(reader, OptionsFlags.ScreenShotKey);
                ReadOption(reader, OptionsFlags.CloseSec);
                Priority = reader.ReadUInt32();
                ReadOption(reader, OptionsFlags.Freeze);
                ReadOption(reader, OptionsFlags.ShowProgress);
                SplashBackImage = reader.ReadPointerObject<GMTextureItem>();
                SplashFrontImage = reader.ReadPointerObject<GMTextureItem>();
                SplashLoadImage = reader.ReadPointerObject<GMTextureItem>();
                ReadOption(reader, OptionsFlags.LoadTransparent);
                LoadAlpha = reader.ReadUInt32();
                ReadOption(reader, OptionsFlags.ScaleProgress);
                ReadOption(reader, OptionsFlags.DisplayErrors);
                ReadOption(reader, OptionsFlags.WriteErrors);
                ReadOption(reader, OptionsFlags.AbortErrors);
                ReadOption(reader, OptionsFlags.VariableErrors);
                ReadOption(reader, OptionsFlags.CreationEventOrder);
            }
            Constants = new GMList<Constant>();
            Constants.Deserialize(reader);
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

            public void Deserialize(GMDataReader reader)
            {
                Name = reader.ReadStringPointerObject();
                Value = reader.ReadStringPointerObject();
            }
        }
    }
}
