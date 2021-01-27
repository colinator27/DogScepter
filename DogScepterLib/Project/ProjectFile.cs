using DogScepterLib.Core;
using DogScepterLib.Core.Chunks;
using DogScepterLib.Core.Models;
using DogScepterLib.Project.Assets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DogScepterLib.Project
{
    public class ProjectFile
    {
        public string FilePath;
        public GMData DataHandle;

        public HashSet<string> AssetNames = new HashSet<string>();

        public AssetList<AssetPath> Paths;

        public static JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        public ProjectFile(GMData dataHandle, string filePath)
        {
            DataHandle = dataHandle;
            FilePath = filePath;

            ProjectFileJson json = null;
            if (File.Exists(filePath))
                json = ReadMainFile();

            LoadPaths(json);
        }

        public void RebuildData()
        {
            RebuildPaths();
        }

        private void RebuildPaths()
        {
            IList<GMPath> dataPaths = ((GMChunkPATH)DataHandle.Chunks["PATH"]).List;
            dataPaths.Clear();
            foreach (var p in Paths)
            {
                GMPath newPath = new GMPath()
                {
                    Name = DataHandle.DefineString(p.Asset.Name),
                    Smooth = p.Asset.Smooth,
                    Closed = p.Asset.Closed,
                    Precision = p.Asset.Precision,
                    Points = new GMList<GMPath.Point>()
                };
                foreach (AssetPath.Point point in p.Asset.Points)
                    newPath.Points.Add(new GMPath.Point() { X = point.X, Y = point.Y, Speed = point.Speed });
            }
        }

        private void LoadPaths(ProjectFileJson json)
        {
            IList<GMPath> dataPaths = ((GMChunkPATH)DataHandle.Chunks["PATH"]).List;

            // Collect IDs of existing assets in the data file
            Dictionary<string, int> dataPathIndices = new Dictionary<string, int>();
            for (int i = 0; i < dataPaths.Count; i++)
                dataPathIndices[dataPaths[i].Name.Content] = i;

            Paths = new AssetList<AssetPath>();

            // Load paths from project file
            if (json != null)
            {
                foreach (var p in json.Paths)
                {
                    int ind;
                    if (dataPathIndices.TryGetValue(p.Name, out ind))
                        Paths.Put(ind, AssetPath.Load(Path.Combine(Path.GetDirectoryName(FilePath), p.Path), p.Name), p.Path, true);
                    else
                        Paths.Put(AssetPath.Load(Path.Combine(Path.GetDirectoryName(FilePath), p.Path), p.Name), p.Path, true);
                    if (!AssetNames.Add(p.Name))
                        throw new Exception($"Duplicate asset name \"${p.Name}\"");
                }
            }

            // Load paths from data
            for (int i = 0; i < dataPaths.Count; i++)
            {
                GMPath p = dataPaths[i];

                if (!AssetNames.Add(p.Name.Content))
                    continue;

                AssetPath newPath = new AssetPath()
                {
                    Name = p.Name.Content,
                    Smooth = p.Smooth,
                    Closed = p.Closed,
                    Precision = p.Precision,
                    Points = new List<AssetPath.Point>()
                };
                foreach (GMPath.Point point in p.Points)
                    newPath.Points.Add(new AssetPath.Point() { X = point.X, Y = point.Y, Speed = point.Speed });

                Paths.Put(i, newPath, "paths/" + p.Name.Content + "/", false);
            }

            Paths.Finish();
        }

        private class ProjectFileJson
        {
            public struct AssetRefJson
            {
                public string Name { get; set; }
                public string Path { get; set; }
            }

            public List<AssetRefJson> Paths { get; set; } = new List<AssetRefJson>();
        }

        private ProjectFileJson ReadMainFile()
        {
            return JsonSerializer.Deserialize<ProjectFileJson>(File.ReadAllBytes(FilePath), JsonOptions);
        }

        public void WriteMainFile()
        {
            ProjectFileJson res = new ProjectFileJson();
            foreach (var path in Paths)
            {
                if (path.OnDisk)
                {
                    res.Paths.Add(new ProjectFileJson.AssetRefJson()
                    {
                        Name = path.Asset.Name,
                        Path = path.RelativePath
                    });
                }
            }

            byte[] buff = JsonSerializer.SerializeToUtf8Bytes(res, res.GetType(), JsonOptions);
            using (FileStream fs = new FileStream(FilePath, FileMode.Create))
            {
                fs.Write(buff, 0, buff.Length);
            }
        }

        public void ExportAllPaths()
        {
            for (int i = 0; i < Paths.Count; i++)
            {
                AssetRef<AssetPath> path = Paths[i];
                string loc = Path.Combine(Path.GetDirectoryName(FilePath), path.RelativePath);
                Directory.CreateDirectory(loc);
                path.Asset.Write(loc, path.Asset.Name);
                path.OnDisk = true;
            }
        }
    }
}
