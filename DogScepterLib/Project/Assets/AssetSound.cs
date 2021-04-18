using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace DogScepterLib.Project.Assets
{
    public class AssetSound : Asset
    {
        public enum Attribute
        {
            Uncompressed,
            CompressedNotStreamed,
            UncompressOnLoad,
            CompressedStreamed
        }
        public float Volume { get; set; }
        public float Pitch { get; set; }
        public Attribute Attributes { get; set; }
        public string OriginalSoundFile { get; set; }
        public string SoundFile { get; set; }
        public string Type { get; set; }
        public string AudioGroup { get; set; }
        public byte[] SoundFileBuffer;

        public new static Asset Load(string assetPath)
        {
            byte[] buff = File.ReadAllBytes(assetPath);
            var res = JsonSerializer.Deserialize<AssetSound>(buff, ProjectFile.JsonOptions);
            ComputeHash(res, buff);

            // Involve hash of sound file as well
            string soundFilePath = Path.Combine(Path.GetDirectoryName(assetPath), res.SoundFile);
            if (File.Exists(soundFilePath))
            {
                // Load the sound file's data, and also calculate combined hash
                byte[] oldHash = res.Hash;
                int oldLength = res.Length;

                res.SoundFileBuffer = File.ReadAllBytes(soundFilePath);
                ComputeHash(res, res.SoundFileBuffer);

                buff = new byte[20 + 4 + 20 + 4];
                for (int i = 0; i < 20; i++)
                    buff[i] = oldHash[i];
                buff[20] = (byte)(oldLength & 0xFF);
                buff[21] = (byte)((oldLength >> 8) & 0xFF);
                buff[22] = (byte)((oldLength >> 16) & 0xFF);
                buff[23] = (byte)((oldLength >> 24) & 0xFF);
                for (int i = 24; i < 24 + 20; i++)
                    buff[i] = res.Hash[i - 24];
                buff[44] = (byte)(res.Length & 0xFF);
                buff[45] = (byte)((res.Length >> 8) & 0xFF);
                buff[46] = (byte)((res.Length >> 16) & 0xFF);
                buff[47] = (byte)((res.Length >> 24) & 0xFF);

                ComputeHash(res, buff);
            }

            return res;
        }

        protected override byte[] WriteInternal(ProjectFile pf, string assetPath, bool actuallyWrite)
        {
            byte[] buff = JsonSerializer.SerializeToUtf8Bytes(this, GetType(), ProjectFile.JsonOptions);
            if (actuallyWrite)
            {
                string dir = Path.GetDirectoryName(assetPath);
                Directory.CreateDirectory(dir);

                using (FileStream fs = new FileStream(assetPath, FileMode.Create))
                    fs.Write(buff, 0, buff.Length);

                if (SoundFileBuffer != null)
                {
                    using (FileStream fs = new FileStream(Path.Combine(dir, SoundFile), FileMode.Create))
                        fs.Write(SoundFileBuffer, 0, SoundFileBuffer.Length);
                }
            }

            if (SoundFileBuffer != null)
            {
                ComputeHash(this, buff);

                byte[] oldHash = Hash;
                int oldLength = Length;

                ComputeHash(this, SoundFileBuffer);

                buff = new byte[20 + 4 + 20 + 4];
                for (int i = 0; i < 20; i++)
                    buff[i] = oldHash[i];
                buff[20] = (byte)(oldLength & 0xFF);
                buff[21] = (byte)((oldLength >> 8) & 0xFF);
                buff[22] = (byte)((oldLength >> 16) & 0xFF);
                buff[23] = (byte)((oldLength >> 24) & 0xFF);
                for (int i = 24; i < 24 + 20; i++)
                    buff[i] = Hash[i - 24];
                buff[44] = (byte)(Length & 0xFF);
                buff[45] = (byte)((Length >> 8) & 0xFF);
                buff[46] = (byte)((Length >> 16) & 0xFF);
                buff[47] = (byte)((Length >> 24) & 0xFF);
            }

            return buff;
        }

        public override void Delete(string assetPath)
        {
            if (File.Exists(assetPath))
                File.Delete(assetPath);
            string dir = Path.GetDirectoryName(assetPath);
            string soundFilePath = Path.Combine(dir, SoundFile);
            if (File.Exists(soundFilePath))
                File.Delete(soundFilePath);

            if (Directory.Exists(dir))
                Directory.Delete(dir);
        }
    }
}
