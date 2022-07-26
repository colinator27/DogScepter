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
using DogScepterLib.Project.GML.Compiler;

namespace DogScepterLib.Project.Assets
{
    public class AssetCode : Asset
    {
        public class CodePatch
        {
            public string Filename { get; set; }
            public CodeContext.CodeMode Mode { get; set; }
            public string Code;
        }

        public bool IsScript { get; set; }
        public List<CodePatch> Patches { get; set; }

        public new static Asset Load(string assetPath)
        {
            byte[] buff = File.ReadAllBytes(assetPath);
            var res = JsonSerializer.Deserialize<AssetCode>(buff, ProjectFile.JsonOptions);

            string dir = Path.GetDirectoryName(assetPath);

            using (var sha1 = SHA1.Create())
            {
                res.Length = buff.Length;

                // Load GML code
                foreach (var patch in res.Patches)
                {
                    string path = Path.Combine(dir, patch.Filename);
                    if (File.Exists(path))
                    {
                        byte[] codeBuff = File.ReadAllBytes(path);
                        patch.Code = Encoding.UTF8.GetString(codeBuff, 0, codeBuff.Length);

                        sha1.TransformBlock(codeBuff, 0, codeBuff.Length, null, 0);
                        res.Length += codeBuff.Length;
                    }
                }

                sha1.TransformFinalBlock(buff, 0, buff.Length);
                res.Hash = sha1.Hash;
            }

            return res;
        }

        public override void Delete(string assetPath)
        {
            if (File.Exists(assetPath))
                File.Delete(assetPath);
            foreach (var patch in Patches)
            {
                string codePath = Path.Combine(Path.GetDirectoryName(assetPath), patch.Filename);
                if (File.Exists(codePath))
                    File.Delete(codePath);
            }

            // todo: directory if wanted?
        }

        protected override byte[] WriteInternal(ProjectFile pf, string assetPath, bool actuallyWrite)
        {
            byte[] buff = JsonSerializer.SerializeToUtf8Bytes(this, ProjectFile.JsonOptions);

            string dir = null;
            if (actuallyWrite)
            {
                dir = Path.GetDirectoryName(assetPath);
                Directory.CreateDirectory(dir);
                using (FileStream fs = new FileStream(assetPath, FileMode.Create))
                    fs.Write(buff, 0, buff.Length);
            }

            // Compute hash manually here
            using (var sha1 = SHA1.Create())
            {
                Length = buff.Length;

                // Write GML code
                foreach (var patch in Patches)
                {
                    byte[] code = Encoding.UTF8.GetBytes(patch.Code);

                    if (actuallyWrite)
                    {
                        using FileStream fs = new FileStream(Path.Combine(dir, patch.Filename), FileMode.Create);
                        fs.Write(code, 0, code.Length);
                    }

                    Length += code.Length;
                    sha1.TransformBlock(code, 0, code.Length, null, 0);
                }

                sha1.TransformFinalBlock(buff, 0, buff.Length);
                Hash = sha1.Hash;
            }

            return null;
        }
    }
}
