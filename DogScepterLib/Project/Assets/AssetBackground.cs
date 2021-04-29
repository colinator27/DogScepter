using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DogScepterLib.Project.Assets
{
    class AssetBackground : Asset
    {
        public bool Transparent { get; set; }
        public bool Smooth { get; set; }
        public bool Preload { get; set; }
        public TileInfo GMS2Tiles { get; set; }

        public GMTextureItem TextureItem;

        public new static Asset Load(string assetPath)
        {
            byte[] buff = File.ReadAllBytes(assetPath);
            var res = JsonSerializer.Deserialize<AssetObject>(buff, ProjectFile.JsonOptions);
            ComputeHash(res, buff);
            return res;
        }

        protected override byte[] WriteInternal(ProjectFile pf, string assetPath, bool actuallyWrite)
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

            string dir = Path.GetDirectoryName(assetPath);
            if (Directory.Exists(dir))
                Directory.Delete(dir);
        }

        public struct TileInfo
        {
            public uint Width { get; set; }
            public uint Height { get; set; }
            public uint BorderX;
            public uint BorderY;
            public uint Columns;
            public long FrameLength;
            public List<List<uint>> Tiles;
        }
    }
}
