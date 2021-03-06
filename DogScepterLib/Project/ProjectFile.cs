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
        public enum WarningType
        {
            DataFileMismatch
        }
        public delegate void Warning(WarningType type);

        public string DirectoryPath;
        public GMData DataHandle;
        public Warning WarningHandler;

        public ProjectJson JsonFile;
        public List<AssetPath> Paths;

        public static JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        public ProjectFile(GMData dataHandle, string directoryPath, Warning warningHandler = null)
        {
            DataHandle = dataHandle;
            DirectoryPath = directoryPath;
            WarningHandler = warningHandler;

            JsonFile = new ProjectJson();

            ConvertDataToProject.Convert(this);
        }

        public void Save()
        {
            Directory.CreateDirectory(DirectoryPath);

            SavePaths();

            File.WriteAllBytes(Path.Combine(DirectoryPath, "project.json"), JsonSerializer.SerializeToUtf8Bytes(JsonFile, JsonOptions));
        }

        public void Load()
        {
            JsonFile = JsonSerializer.Deserialize<ProjectJson>(
                File.ReadAllBytes(Path.Combine(DirectoryPath, "project.json")),
                JsonOptions);

            // Compare data file for mismatch
            unsafe
            {
                fixed (byte* a = DataHandle.Hash, b = JsonFile.BaseFileHash)
                {
                    int* ai = (int*)a, bi = (int*)b;
                    if (DataHandle.Length != JsonFile.BaseFileLength || ai[0] != bi[0] || ai[1] != bi[1] || ai[2] != bi[2] || ai[3] != bi[3] || ai[4] != bi[4])
                        WarningHandler?.Invoke(WarningType.DataFileMismatch);
                }
            }

            LoadPaths();
        }

        public void ConvertToData()
        {
            Load();
            ConvertProjectToData.Convert(this);
        }

        private void SavePaths()
        {
            // Collect IDs of existing assets in project
            Dictionary<string, int> dataPathIndices = new Dictionary<string, int>();
            for (int i = 0; i < Paths.Count; i++)
                dataPathIndices[Paths[i].Name] = i;

            foreach (ProjectJson.AssetEntry entry in JsonFile.Assets["Paths"])
            {
                int ind;
                if (dataPathIndices.TryGetValue(entry.Name, out ind))
                {
                    var asset = Paths[ind];
                    asset.Write(Path.Combine(DirectoryPath, entry.Path));
                }
            }
        }

        private void LoadPaths()
        {
            // Collect IDs of existing assets in project
            Dictionary<string, int> dataPathIndices = new Dictionary<string, int>();
            for (int i = 0; i < Paths.Count; i++)
                dataPathIndices[Paths[i].Name] = i;

            foreach (ProjectJson.AssetEntry entry in JsonFile.Assets["Paths"])
            {
                if (dataPathIndices.ContainsKey(entry.Name))
                    Paths[dataPathIndices[entry.Name]] = AssetPath.Load(Path.Combine(DirectoryPath, entry.Path));
                else
                    Paths.Add(AssetPath.Load(Path.Combine(DirectoryPath, entry.Path)));
            }
        }

        public void AddAllPathsToJSON()
        {
            JsonFile.Assets["Paths"] = new List<ProjectJson.AssetEntry>();
            foreach (var asset in Paths)
            {
                JsonFile.Assets["Paths"].Add(new ProjectJson.AssetEntry()
                {
                    Name = asset.Name,
                    Path = $"paths/{asset.Name}.json"
                });
            }
        }

        public void PurgeUnmodifiedPaths()
        {
            var unmodifiedList = ConvertDataToProject.ConvertPaths(DataHandle);
            Dictionary<string, AssetPath> unmodifiedNames = new Dictionary<string, AssetPath>();
            foreach (var path in unmodifiedList)
            {
                path.ComputeHash();
                unmodifiedNames[path.Name] = path;
            }

            // Collect IDs of existing assets in project
            Dictionary<string, int> dataPathIndices = new Dictionary<string, int>();
            for (int i = 0; i < Paths.Count; i++)
                dataPathIndices[Paths[i].Name] = i;

            List<ProjectJson.AssetEntry> entries = JsonFile.Assets["Paths"];
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                ProjectJson.AssetEntry entry = entries[i];
                if (dataPathIndices.ContainsKey(entry.Name))
                {
                    AssetPath path = Paths[dataPathIndices[entry.Name]];
                    AssetPath other;
                    if (unmodifiedNames.TryGetValue(path.Name, out other))
                    {
                        if (path.CompareHash(other))
                        {
                            // Identical. Purge!
                            File.Delete(Path.Combine(DirectoryPath, entry.Path));
                            entries.RemoveAt(i);
                        }
                    }
                }
            }

            Save();
        }
    }

    public class ProjectJson
    {
        public struct AssetEntry
        {
            public string Name { get; set; }
            public string Path { get; set; }
        }

        public int Version { get; private set; } = 1;
        public int BaseFileLength { get; set; }
        public byte[] BaseFileHash { get; set; }
        public Dictionary<string, object> Info { get; set; }
        public Dictionary<string, List<AssetEntry>> Assets { get; set; }

        public ProjectJson()
        {
            Assets = new Dictionary<string, List<AssetEntry>>();
            Assets.Add("Paths", new List<AssetEntry>());
        }
    }
}
