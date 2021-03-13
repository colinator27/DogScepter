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
            DataFileMismatch,
            MissingAsset
        }
        public delegate void Warning(WarningType type, string info = null);

        public string DirectoryPath;
        public GMData DataHandle;
        public Warning WarningHandler;

        public ProjectJson JsonFile;
        public List<AssetPath> Paths;
        public List<AssetSound> Sounds;
        public List<AssetObject> Objects;

        private delegate List<Asset> _convertToProjDelegateFunc(GMData data);
        private static Delegate _convertToProjDelegate(string funcName) =>
            Delegate.CreateDelegate(typeof(_convertToProjDelegateFunc), typeof(ConvertDataToProject), funcName);
                
        protected readonly static Dictionary<Type, Delegate> AssetTypeConvertToProject = new Dictionary<Type, Delegate>()
        {
            { typeof(AssetPath), _convertToProjDelegate("ConvertPaths") },
            { typeof(AssetSound), _convertToProjDelegate("ConvertSounds") },
            { typeof(AssetObject), _convertToProjDelegate("ConvertObjects") },
        };

        // From https://github.com/dotnet/runtime/issues/33112
        [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false)]
        public class JsonInterfaceConverterAttribute : JsonConverterAttribute
        {
            public JsonInterfaceConverterAttribute(Type converterType)
                : base(converterType)
            {
            }
        }
        public static JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            Converters = { new JsonStringEnumConverter() },
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
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

            SaveAssets(Paths);
            SaveAssets(Sounds);
            SaveAssets(Objects);

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

            LoadAssets(Paths);
            LoadAssets(Sounds);
            LoadAssets(Objects);
        }

        /// <summary>
        /// Converts this project file to the GameMaker format
        /// </summary>
        public void ConvertToData()
        {
            Load();
            ConvertProjectToData.Convert(this);
        }

        /// <summary>
        /// Writes out all assets in main project JSON
        /// </summary>
        private void SaveAssets<T>(List<T> list) where T : Asset
        {
            // Collect IDs of existing assets in project
            Dictionary<string, int> assetIndices = new Dictionary<string, int>();
            for (int i = 0; i < list.Count; i++)
                assetIndices[list[i].Name] = i;

            // Write them out
            foreach (ProjectJson.AssetEntry entry in JsonFile.Assets[ProjectJson.AssetTypeName[typeof(T)]])
            {
                int ind;
                if (assetIndices.TryGetValue(entry.Name, out ind))
                {
                    T asset = list[ind];
                    asset.Write(Path.Combine(DirectoryPath, entry.Path));
                }
            }
        }

        /// <summary>
        /// Loads assets from disk, using the JSON entries
        /// </summary>
        private void LoadAssets<T>(List<T> list) where T : Asset
        {
            // Collect IDs of existing assets in project
            Dictionary<string, int> assetIndices = new Dictionary<string, int>();
            for (int i = 0; i < list.Count; i++)
                assetIndices[list[i].Name] = i;

            // Now load the assets from disk
            var loadMethod = typeof(T).GetMethod("Load");
            foreach (ProjectJson.AssetEntry entry in JsonFile.Assets[ProjectJson.AssetTypeName[typeof(T)]])
            {
                string path = Path.Combine(DirectoryPath, entry.Path);
                if (!File.Exists(path))
                {
                    WarningHandler.Invoke(WarningType.MissingAsset, entry.Name);
                    continue;
                }

                if (assetIndices.ContainsKey(entry.Name))
                    list[assetIndices[entry.Name]] = (T)loadMethod.Invoke(null, new object[] { path });
                else
                    list.Add((T)loadMethod.Invoke(null, new object[] { path }));
            }
        }

        /// <summary>
        /// Adds all assets of this type in the project to the JSON; essentially marking them to be exported
        /// </summary>
        public void AddAllAssetsToJSON<T>(List<T> list, string assetDir) where T : Asset
        {
            string jsonName = ProjectJson.AssetTypeName[typeof(T)];
            JsonFile.Assets[jsonName] = new List<ProjectJson.AssetEntry>();
            if (ProjectJson.AssetUsesFolder.Contains(typeof(T)))
            {
                // Use a folder, with a JSON inside of it
                foreach (T asset in list)
                {
                    JsonFile.Assets[jsonName].Add(new ProjectJson.AssetEntry()
                    {
                        Name = asset.Name,
                        Path = $"{assetDir}/{asset.Name}/{asset.Name}.json"
                    });
                }
            }
            else
            {
                // Use a direct JSON file
                foreach (T asset in list)
                {
                    JsonFile.Assets[jsonName].Add(new ProjectJson.AssetEntry()
                    {
                        Name = asset.Name,
                        Path = $"{assetDir}/{asset.Name}.json"
                    });
                }
            }
        }

        public void PurgeUnmodifiedAssets<T>(List<T> list) where T : Asset
        {
            // Compute hashes of unmodified asset list
            List<Asset> unmodifiedList = (List<Asset>)AssetTypeConvertToProject[typeof(T)].Method.Invoke(null, new object[] { DataHandle });
            Dictionary<string, T> unmodifiedNames = new Dictionary<string, T>();
            foreach (T asset in unmodifiedList)
            {
                asset.ComputeHash();
                unmodifiedNames[asset.Name] = asset;
            }

            // Collect IDs of existing assets in project
            Dictionary<string, int> assetIndices = new Dictionary<string, int>();
            for (int i = 0; i < list.Count; i++)
                assetIndices[list[i].Name] = i;

            // Now scan through and delete relevant files on disk
            List<ProjectJson.AssetEntry> entries = JsonFile.Assets[ProjectJson.AssetTypeName[typeof(T)]];
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                ProjectJson.AssetEntry entry = entries[i];
                if (assetIndices.ContainsKey(entry.Name))
                {
                    T asset = list[assetIndices[entry.Name]];
                    T other;
                    if (unmodifiedNames.TryGetValue(asset.Name, out other))
                    {
                        if (asset.CompareHash(other))
                        {
                            // Identical. Purge!
                            asset.Delete(Path.Combine(DirectoryPath, entry.Path));
                            entries.RemoveAt(i);
                        }
                    }
                }
            }

            // Save the main JSON file to reflect the removed assets
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
        public List<string> AudioGroups { get; set; }
        public Dictionary<string, List<AssetEntry>> Assets { get; set; }


        public readonly static Dictionary<Type, string> AssetTypeName = new Dictionary<Type, string>()
        {
            { typeof(AssetPath), "Paths" },
            { typeof(AssetSound), "Sounds" },
            { typeof(AssetObject), "Objects" },
        };
        public readonly static HashSet<Type> AssetUsesFolder = new HashSet<Type>()
        {
            typeof(AssetSound),
            typeof(AssetObject), // Code entries not implemented yet, will be eventually
        };

        public ProjectJson()
        {
            Assets = new Dictionary<string, List<AssetEntry>>();
            foreach (var pair in AssetTypeName)
                Assets.Add(pair.Value, new List<AssetEntry>());
        }
    }
}
