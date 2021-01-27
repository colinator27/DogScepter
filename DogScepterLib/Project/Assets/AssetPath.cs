using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace DogScepterLib.Project.Assets
{
    public class AssetPath : Asset
    {
        public string Name { get; set; }
        public bool Smooth { get; set; }
        public bool Closed { get; set; }
        public uint Precision { get; set; }
        public List<Point> Points { get; set; }

        public static AssetPath Load(string assetDir, string assetName)
        {
            return JsonSerializer.Deserialize<AssetPath>(
                File.ReadAllBytes(Path.Combine(assetDir, assetName + ".json")), 
                    ProjectFile.JsonOptions);
        }

        protected override byte[] WriteInternal(string assetDir, string assetName)
        {
            byte[] buff = JsonSerializer.SerializeToUtf8Bytes(this, GetType(), ProjectFile.JsonOptions);
            using (FileStream fs = new FileStream(Path.Combine(assetDir, assetName + ".json"), FileMode.Create))
            {
                fs.Write(buff, 0, buff.Length);
            }
            return null; // Hashing not required for paths, no performance benefit
        }

        public struct Point
        {
            public float X { get; set; }
            public float Y { get; set; }
            public float Speed { get; set; }
        }
    }
}
