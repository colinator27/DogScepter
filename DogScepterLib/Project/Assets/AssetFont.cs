using DogScepterLib.Core.Models;
using System.Drawing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using DogScepterLib.Project.Util;

namespace DogScepterLib.Project.Assets
{
    public class AssetFont : Asset
    {
        public string DisplayName { get; set; }
        public int? Size { get; set; }
        public float? SizeFloat { get; set; }
        public bool Bold { get; set; }
        public bool Italic { get; set; }
        public byte Charset { get; set; }
        public byte AntiAlias { get; set; }
        public float ScaleX { get; set; }
        public float ScaleY { get; set; }
        public int? AscenderOffset { get; set; }
        public int? Ascender { get; set; }
        public string TextureGroup { get; set; } = "";
        public List<GMGlyph> Glyphs { get; set; }

        public GMTextureItem TextureItem;

        public new static Asset Load(string assetPath)
        {
            byte[] buff = File.ReadAllBytes(assetPath);
            var res = JsonSerializer.Deserialize<AssetFont>(buff, ProjectFile.JsonOptions);

            // Order the glyphs automatically
            res.Glyphs = res.Glyphs.OrderBy(g => g.Character).ToList();

            string pngPath = Path.Combine(Path.GetDirectoryName(assetPath), res.Name + ".png");
            if (File.Exists(pngPath))
            {
                // Load PNG file, make new texture item for it
                DSImage img = new DSImage(pngPath);
                res.TextureItem = new GMTextureItem(img);
                res.TextureItem._EmptyBorder = true;

                // Compute hash manually here
                using var sha1 = SHA1.Create();
                sha1.TransformBlock(buff, 0, buff.Length, null, 0);
                sha1.TransformFinalBlock(img.Data, 0, img.Data.Length);
                res.Length = buff.Length + img.Data.Length;
                res.Hash = sha1.Hash;
            }
            else
            {
                // Compute hash manually here, but without image.
                // This isn't intended but we won't error out just yet
                using (var sha1 = SHA1.Create())
                {
                    sha1.TransformFinalBlock(buff, 0, buff.Length);
                    res.Length = buff.Length;
                    res.Hash = sha1.Hash;
                }
            }

            return res;
        }

        protected override byte[] WriteInternal(ProjectFile pf, string assetPath, bool actuallyWrite)
        {
            byte[] buff = JsonSerializer.SerializeToUtf8Bytes(this, ProjectFile.JsonOptions);
            DSImage img;
            if (TextureItem.TexturePageID == -1)
                img = TextureItem._Image;
            else
                img = pf.Textures.GetTextureEntryImage(TextureItem);

            if (actuallyWrite)
            {
                string dir = Path.GetDirectoryName(assetPath);
                Directory.CreateDirectory(dir);
                using (FileStream fs = new FileStream(assetPath, FileMode.Create))
                    fs.Write(buff, 0, buff.Length);
                using (FileStream fs = new FileStream(Path.Combine(dir, Name + ".png"), FileMode.Create))
                    img.SavePng(fs);
            }

            // Compute hash manually here
            using (var sha1 = SHA1.Create())
            {
                sha1.TransformBlock(buff, 0, buff.Length, null, 0);
                sha1.TransformFinalBlock(img.Data, 0, img.Data.Length);
                Length = buff.Length + img.Data.Length;
                Hash = sha1.Hash;
            }
            return null;
        }

        public override void Delete(string assetPath)
        {
            if (File.Exists(assetPath))
                File.Delete(assetPath);
            string pngPath = Path.Combine(Path.GetDirectoryName(assetPath), Name + ".png");
            if (File.Exists(pngPath))
                File.Delete(pngPath);

            string dir = Path.GetDirectoryName(assetPath);
            if (Directory.Exists(dir))
                Directory.Delete(dir);
        }
    }
}
