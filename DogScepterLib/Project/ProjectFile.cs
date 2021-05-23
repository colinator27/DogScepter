using DogScepterLib.Core;
using DogScepterLib.Core.Chunks;
using DogScepterLib.Core.Models;
using DogScepterLib.Project.Assets;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks.Dataflow;

namespace DogScepterLib.Project
{
    public class ProjectFile : INotifyPropertyChanged
    {
        // Note: This is handled by Fody.PropertyChanged entirely, so no manual work has to be done
        public event PropertyChangedEventHandler PropertyChanged;

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
        public Dictionary<string, object> Info { get; set; } = new Dictionary<string, object>();

        public List<string> AudioGroups { get; set; }
        public List<ProjectJson.TextureGroup> TextureGroups { get; set; }
        public List<AssetRef<AssetPath>> Paths { get; set; } = new List<AssetRef<AssetPath>>();
        public List<AssetRef<AssetSound>> Sounds { get; set; } = new List<AssetRef<AssetSound>>();
        public List<AssetRef<AssetBackground>> Backgrounds { get; set; } = new List<AssetRef<AssetBackground>>();
        public List<AssetRef<AssetObject>> Objects { get; set; } = new List<AssetRef<AssetObject>>();

        public Dictionary<int, GMChunkAUDO> _CachedAudioChunks;
        public Textures Textures;

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

            DataHandle.Logger?.Invoke($"Initializing ProjectFile at {directoryPath}");

            DataHandle.Logger?.Invoke($"Setting up textures...");
            Textures = new Textures(this);

            DataHandle.Logger?.Invoke($"Performing fast conversion...");
            ConvertDataToProject.FastConvert(this);
        }

        public void SaveMain()
        {
            DataHandle.Logger?.Invoke("Saving project JSON...");

            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllBytes(Path.Combine(DirectoryPath, "project.json"), JsonSerializer.SerializeToUtf8Bytes(JsonFile, JsonOptions));
        }

        // Helper function to save a special JSON file
        public delegate byte[] SerializeJson();
        public void SaveExtraJSON(string filename, SerializeJson serialize)
        {
            if (filename == null || filename.Trim() == "")
                return;

            DataHandle.Logger?.Invoke($"Saving \"{filename}\"...");

            string path = Path.Combine(DirectoryPath, filename);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, serialize());
        }

        public void SaveAll()
        {
            SaveMain();
            SaveExtraJSON(JsonFile.Info, () => JsonSerializer.SerializeToUtf8Bytes(Info, JsonOptions));
            if (AudioGroups != null)
                SaveExtraJSON(JsonFile.AudioGroups, () => JsonSerializer.SerializeToUtf8Bytes(AudioGroups, JsonOptions));
            SaveExtraJSON(JsonFile.TextureGroups, () => JsonSerializer.SerializeToUtf8Bytes(TextureGroups, JsonOptions));

            DataHandle.Logger?.Invoke("Saving assets...");

            SaveAssets(Paths);
            SaveAssets(Sounds);
            SaveAssets(Backgrounds);
            SaveAssets(Objects);
        }

        public void LoadMain()
        {
            DataHandle.Logger?.Invoke("Loading project JSON...");

            JsonFile = JsonSerializer.Deserialize<ProjectJson>(
                File.ReadAllBytes(Path.Combine(DirectoryPath, "project.json")),
                JsonOptions);

            // Check if this project file should be checking for a specific file
            if (JsonFile.BaseFileLength > 0 && JsonFile.BaseFileHash != null && JsonFile.BaseFileHash.Length != 0)
            {
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
            }
        }

        // Helper function to load a special JSON file
        public void LoadExtraJSON(string filename, Action<byte[]> deserialize, Action convert)
        {
            if (filename == null || filename.Trim() == "")
            {
                convert();
                return;
            }

            string path = Path.Combine(DirectoryPath, filename);
            if (!File.Exists(path))
            {
                convert();
                return;
            }

            DataHandle.Logger?.Invoke($"Loading \"{filename}\"...");

            deserialize(File.ReadAllBytes(path));
        }

        public void LoadAll()
        {
            LoadMain();
            LoadExtraJSON(JsonFile.Info,
                b => Info = JsonSerializer.Deserialize<Dictionary<string, object>>(b, JsonOptions),
                () => Info = ConvertDataToProject.ConvertInfo(this));
            LoadExtraJSON(JsonFile.AudioGroups,
                b => AudioGroups = JsonSerializer.Deserialize<List<string>>(b, JsonOptions),
                () => AudioGroups = ConvertDataToProject.ConvertAudioGroups(this));
            LoadExtraJSON(JsonFile.TextureGroups,
                b => TextureGroups = JsonSerializer.Deserialize<List<ProjectJson.TextureGroup>>(b, JsonOptions),
                () => TextureGroups = ConvertDataToProject.ConvertTextureGroups(this));

            DataHandle.Logger?.Invoke("Loading assets...");

            LoadAssets(Paths);
            LoadAssets(Sounds);
            LoadAssets(Backgrounds);
            LoadAssets(Objects);
        }

        /// <summary>
        /// Converts this project file to the GameMaker format
        /// </summary>
        public void ConvertToData()
        {
            DataHandle.Logger?.Invoke("Converting ProjectFile back to data...");

            ConvertProjectToData.Convert(this);
        }

        /// <summary>
        /// Writes out all assets in main project JSON
        /// </summary>
        private void SaveAssets<T>(List<AssetRef<T>> list) where T : Asset
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
                    T asset = list[ind].Asset;
                    if (asset != null && asset.Dirty)
                    {
                        asset.Write(this, Path.Combine(DirectoryPath, entry.Path));
                        asset.Dirty = false;
                    }
                }
            }
        }

        /// <summary>
        /// Loads assets from disk, using the JSON entries
        /// </summary>
        private void LoadAssets<T>(List<AssetRef<T>> list) where T : Asset
        {
            // Collect IDs of existing assets in project
            Dictionary<string, int> assetIndices = new Dictionary<string, int>();
            for (int i = 0; i < list.Count; i++)
                assetIndices[list[i].Name] = i;

            // Now load the assets from disk
            var loadMethod = typeof(T).GetMethod("Load");
            if (JsonFile.Assets.TryGetValue(ProjectJson.AssetTypeName[typeof(T)], out var jsonList))
            {
                foreach (ProjectJson.AssetEntry entry in jsonList)
                {
                    string path = Path.Combine(DirectoryPath, entry.Path);
                    if (!File.Exists(path))
                    {
                        WarningHandler.Invoke(WarningType.MissingAsset, entry.Name);
                        continue;
                    }

                    if (assetIndices.ContainsKey(entry.Name))
                        list[assetIndices[entry.Name]].Asset = (T)loadMethod.Invoke(null, new object[] { path });
                    else
                        list.Add(new AssetRef<T>(entry.Name, (T)loadMethod.Invoke(null, new object[] { path })));
                }
            }
        }

        /// <summary>
        /// Adds all dirty assets of this type in the project to the JSON; essentially marking them to be exported
        /// </summary>
        public void AddDirtyAssetsToJSON<T>(List<AssetRef<T>> list, string assetDir) where T : Asset
        {
            string jsonName = ProjectJson.AssetTypeName[typeof(T)];
            JsonFile.Assets[jsonName] = new List<ProjectJson.AssetEntry>();
            if (ProjectJson.AssetUsesFolder.Contains(typeof(T)))
            {
                // Use a folder, with a JSON inside of it
                foreach (AssetRef<T> asset in list)
                {
                    if (asset.Asset == null || !asset.Asset.Dirty)
                        continue; // Skip if this isn't modified

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
                foreach (AssetRef<T> asset in list)
                {
                    if (asset.Asset == null || !asset.Asset.Dirty)
                        continue; // Skip if this isn't modified

                    JsonFile.Assets[jsonName].Add(new ProjectJson.AssetEntry()
                    {
                        Name = asset.Name,
                        Path = $"{assetDir}/{asset.Name}.json"
                    });
                }
            }
        }

        public void PurgeIdenticalAssetsOnDisk<T>(List<AssetRef<T>> list) where T : Asset
        {
            // Collect IDs of existing assets in project
            Dictionary<string, int> assetIndices = new Dictionary<string, int>();
            for (int i = 0; i < list.Count; i++)
                assetIndices[list[i].Name] = i;

            var loadMethod = typeof(T).GetMethod("Load");

            // Now scan through and delete relevant files on disk
            List<ProjectJson.AssetEntry> entries = JsonFile.Assets[ProjectJson.AssetTypeName[typeof(T)]];
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                ProjectJson.AssetEntry entry = entries[i];
                if (assetIndices.ContainsKey(entry.Name))
                {
                    string path = Path.Combine(DirectoryPath, entry.Path);
                    if (!File.Exists(path))
                    {
                        WarningHandler.Invoke(WarningType.MissingAsset, entry.Name);
                        continue;
                    }

                    T asset = list[assetIndices[entry.Name]].Asset;
                    T assetOnDisk = (T)loadMethod.Invoke(null, new object[] { path });
                    if (asset.CompareHash(assetOnDisk))
                    {
                        // Identical. Purge!
                        asset.Delete(Path.Combine(DirectoryPath, entry.Path));
                        entries.RemoveAt(i);
                    }
                }
            }

            // Save the main JSON file to reflect the removed assets
            SaveMain();
        }
    }

    public class AssetRef<T> where T : Asset
    {
        public string Name { get; set; }
        public T Asset { get; set; } = null;
        public int DataIndex { get; set; } = -1;
        public GMSerializable DataAsset { get; set; } = null;
        public CachedRefData CachedData { get; set; } = null;

        public AssetRef(string name)
        {
            Name = name;
        }

        public AssetRef(string name, T asset, int dataIndex = -1)
        {
            Name = name;
            Asset = asset;
            DataIndex = dataIndex;
        }

        public AssetRef(string name, int dataIndex, GMSerializable dataAsset = null)
        {
            Name = name;
            DataIndex = dataIndex;
            DataAsset = dataAsset;
        }
    }

    public interface CachedRefData
    {
    }

    public class CachedSoundRefData : CachedRefData
    {
        public byte[] SoundBuffer { get; set; }
        public string AudioGroupName { get; set; }

        public CachedSoundRefData(byte[] soundBuffer, string audioGroupName)
        {
            SoundBuffer = soundBuffer;
            AudioGroupName = audioGroupName;
        }
    }

    public class ProjectJson
    {
        public struct AssetEntry
        {
            public string Name { get; set; }
            public string Path { get; set; }
        }

        public struct TextureGroup
        {
            public string Name { get; set; }
            public int Border { get; set; }
            public bool AllowCrop { get; set; }
            public int ID { get; set; } // in ProjectFile.Textures.TextureGroups
        }

        public int Version { get; private set; } = 1;
        public int BaseFileLength { get; set; } = 0;
        public byte[] BaseFileHash { get; set; } = null;
        public string Info { get; set; } // Filename of info JSON
        public string AudioGroups { get; set; } // Filename of audio group JSON
        public string DataFiles { get; set; } = ""; // Folder name of data files
        public string TextureGroups { get; set; } // Filename of texture group JSON
        public Dictionary<string, List<AssetEntry>> Assets { get; set; }


        public readonly static Dictionary<Type, string> AssetTypeName = new Dictionary<Type, string>()
        {
            { typeof(AssetPath), "Paths" },
            { typeof(AssetSound), "Sounds" },
            { typeof(AssetBackground), "Backgrounds" },
            { typeof(AssetObject), "Objects" },
        };
        public readonly static HashSet<Type> AssetUsesFolder = new HashSet<Type>()
        {
            typeof(AssetSound),
            typeof(AssetBackground),
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
