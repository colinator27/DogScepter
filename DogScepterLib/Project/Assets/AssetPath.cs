using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DogScepterLib.Project.Assets
{
    public class AssetPath : Asset
    {
        public bool Smooth { get; set; }
        public bool Closed { get; set; }
        public uint Precision { get; set; }
        public List<Point> Points { get; set; }

        public struct Point
        {
            public float X { get; set; }
            public float Y { get; set; }
            public float Speed { get; set; }
        }

        public new static Asset Load(string assetPath)
        {
            byte[] buff = File.ReadAllBytes(assetPath);
            var res = JsonSerializer.Deserialize<AssetPath>(buff, ProjectFile.JsonOptions);
            ComputeHash(res, buff);
            return res;
        }

        protected override byte[] WriteInternal(string assetPath, bool actuallyWrite)
        {
            byte[] buff = JsonSerializer.SerializeToUtf8Bytes(this, GetType(), ProjectFile.JsonOptions);
            if (actuallyWrite)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
                using (FileStream fs = new FileStream(assetPath, FileMode.Create))
                    fs.Write(buff, 0, buff.Length);
            }
            return buff;
        }

        public override void Delete(string assetPath)
        {
            if (File.Exists(assetPath))
                File.Delete(assetPath);
        }
    }
}
