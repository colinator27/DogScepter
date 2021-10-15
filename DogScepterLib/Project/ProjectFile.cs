using DogScepterLib.Core;
using DogScepterLib.Core.Chunks;
using DogScepterLib.Core.Models;
using DogScepterLib.Project.Assets;
using DogScepterLib.Project.Converters;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using static DogScepterLib.Core.Chunks.GMChunkOPTN;

namespace DogScepterLib.Project
{
    [AddINotifyPropertyChangedInterface]
    public class ProjectFile
    {
        public enum WarningType
        {
            DataFileMismatch,
            MissingAsset,
            MissingAudioGroup
        }
        public delegate void Warning(WarningType type, string info = null);

        public string DirectoryPath;
        public GMData DataHandle;
        public Warning WarningHandler;

        public ProjectJson JsonFile;

        public Dictionary<string, object> Info { get; set; } = new Dictionary<string, object>();
        public ProjectJson.OptionsSettings Options { get; set; }
        public ProjectJson.AudioGroupSettings AudioGroupSettings { get; set; }
        public ProjectJson.TextureGroupSettings TextureGroupSettings { get; set; }

        public AssetRefList<AssetSound> Sounds { get; set; } = new();
        public AssetRefList<AssetSprite> Sprites { get; set; } = new();
        public AssetRefList<AssetBackground> Backgrounds { get; set; } = new();
        public AssetRefList<AssetFont> Fonts { get; set; } = new();
        public AssetRefList<AssetPath> Paths { get; set; } = new();
        public AssetRefList<AssetObject> Objects { get; set; } = new();
        public AssetRefList<AssetRoom> Rooms { get; set; } = new();

        public Dictionary<int, GMChunkAUDO> _CachedAudioChunks;
        public Textures InternalTextures = null;
        public Textures Textures
        {
            get 
            {
                if (InternalTextures == null)
                {
                    DataHandle.Logger?.Invoke($"Setting up textures...");
#if DEBUG
                    Stopwatch s = new Stopwatch();
                    s.Start();
#endif
                    InternalTextures = new Textures(this);
#if DEBUG
                    s.Stop();
                    DataHandle.Logger?.Invoke($"Set up textures in {s.ElapsedMilliseconds} ms");
#endif
                }
                return InternalTextures;
            }
            set
            {
                InternalTextures = value;
            }
        }

        // From https://github.com/dotnet/runtime/issues/33112
        [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false)]
        public class JsonInterfaceConverterAttribute : JsonConverterAttribute
        {
            public JsonInterfaceConverterAttribute(Type converterType)
                : base(converterType)
            {
            }
        }
        public static JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            Converters = { new JsonStringEnumConverter() },
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public List<IConverter> Converters = new()
        {
            new InfoConverter(),
            new OptionsConverter(),
            new AudioGroupConverter(),
            new TextureGroupConverter(),
            new SoundConverter(),
            new SpriteConverter(),
            new BackgroundConverter(),
            new FontConverter(),
            new PathConverter(),
            new ObjectConverter(),
            new RoomConverter(),
        };

        public readonly static Dictionary<Type, Type> AssetTypeConverter = new Dictionary<Type, Type>()
        {
            { typeof(AssetSound), typeof(SoundConverter) },
            { typeof(AssetSprite), typeof(SpriteConverter) },
            { typeof(AssetBackground), typeof(BackgroundConverter) },
            { typeof(AssetFont), typeof(FontConverter) },
            { typeof(AssetObject), typeof(ObjectConverter) },
            { typeof(AssetPath), typeof(PathConverter) },
            { typeof(AssetRoom), typeof(RoomConverter) },
        };

        public T GetConverter<T>() where T : IConverter, new()
        {
            foreach (var converter in Converters)
                if (converter is T t)
                    return t;

            T newConverter = new T();
            Converters.Add(newConverter);
            return newConverter;
        }

        public IConverter GetConverter(Type t)
        {
            foreach (var converter in Converters)
                if (converter.GetType() == t)
                    return converter;

            throw new ArgumentException("Converter does not exist");
        }

        public ProjectFile(GMData dataHandle, string directoryPath, Warning warningHandler = null)
        {
            DataHandle = dataHandle;
            DirectoryPath = directoryPath;
            WarningHandler = warningHandler;

            DataHandle.Logger?.Invoke($"Initializing ProjectFile at {directoryPath}");

            JsonFile = new ProjectJson();

            ConvertFromData();
        }

        // Sets up textures and converts data to the project format
        public void ConvertFromData()
        {
            DataHandle.Logger?.Invoke($"Performing base conversion...");
            foreach (var converter in Converters)
                converter.ConvertData(this);
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
            SaveExtraJSON(JsonFile.Options, () => JsonSerializer.SerializeToUtf8Bytes(Options, JsonOptions));
            if (AudioGroupSettings != null)
                SaveExtraJSON(JsonFile.AudioGroups, () => JsonSerializer.SerializeToUtf8Bytes(AudioGroupSettings, JsonOptions));
            SaveExtraJSON(JsonFile.TextureGroups, () => JsonSerializer.SerializeToUtf8Bytes(TextureGroupSettings, JsonOptions));

            DataHandle.Logger?.Invoke("Saving assets...");

            SaveAssets(Sounds);
            SaveAssets(Sprites);
            SaveAssets(Backgrounds);
            SaveAssets(Fonts);
            SaveAssets(Paths);
            SaveAssets(Objects);
            SaveAssets(Rooms);
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
        public void LoadExtraJSON(string filename, Action<byte[]> deserialize)
        {
            if (filename == null || filename.Trim() == "")
                return;

            string path = Path.Combine(DirectoryPath, filename);
            if (!File.Exists(path))
                return;

            DataHandle.Logger?.Invoke($"Loading \"{filename}\"...");

            deserialize(File.ReadAllBytes(path));
        }

        public void LoadAll()
        {
            LoadMain();
            LoadExtraJSON(JsonFile.Info,
                b => Info = JsonSerializer.Deserialize<Dictionary<string, object>>(b, JsonOptions));
            LoadExtraJSON(JsonFile.Options,
                b => Options = JsonSerializer.Deserialize<ProjectJson.OptionsSettings>(b, JsonOptions));
            LoadExtraJSON(JsonFile.AudioGroups,
                b => AudioGroupSettings = JsonSerializer.Deserialize<ProjectJson.AudioGroupSettings>(b, JsonOptions));
            LoadExtraJSON(JsonFile.TextureGroups,
                b => TextureGroupSettings = JsonSerializer.Deserialize<ProjectJson.TextureGroupSettings>(b, JsonOptions));

            DataHandle.Logger?.Invoke("Loading assets...");

            LoadAssets(Sounds);
            LoadAssets(Sprites);
            LoadAssets(Backgrounds);
            LoadAssets(Fonts);
            LoadAssets(Paths);
            LoadAssets(Objects);
            LoadAssets(Rooms);
        }

        /// <summary>
        /// Converts this project file to the GameMaker format
        /// </summary>
        public void ConvertToData()
        {
            DataHandle.Logger?.Invoke("Converting ProjectFile back to data...");

            if (!Directory.Exists(DataHandle.Directory))
                throw new Exception($"Missing output directory \"{DataHandle.Directory}\"");

            DataHandle.BuildStringCache();

            foreach (var converter in Converters)
                converter.ConvertProject(this);

            Textures.PurgeUnreferencedItems();
            Textures.RefreshTextureGroups();
            Textures.RegenerateTextures();
            Textures.PurgeUnreferencedPages();
            Textures.RefreshTGIN();

            ConverterUtils.CopyDataFiles(this);
        }

        /// <summary>
        /// Writes out all assets in main project JSON
        /// </summary>
        public void SaveAssets<T>(AssetRefList<T> list) where T : Asset
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
        /// Adds supplied indices of an asset list to the JSON; essentially marking them to be exported
        /// </summary>
        public void AddAssetsToJSON<T>(List<AssetRef<T>> list, IEnumerable<int> indices, bool convert = false, string assetDir = null) where T : Asset
        {
            string jsonName = ProjectJson.AssetTypeName[typeof(T)];
            assetDir ??= jsonName.ToLowerInvariant();

            List<ProjectJson.AssetEntry> jsonList;
            if (!JsonFile.Assets.TryGetValue(jsonName, out jsonList))
                JsonFile.Assets[jsonName] = jsonList = new List<ProjectJson.AssetEntry>();

            AssetConverter<T> cvt = null;
            if (convert)
                cvt = (GetConverter(AssetTypeConverter[typeof(T)]) as AssetConverter<T>);

            if (ProjectJson.AssetUsesFolder.Contains(typeof(T)))
            {
                // Use a folder, with a JSON inside of it
                foreach (int assetIndex in indices)
                {
                    AssetRef<T> asset = list[assetIndex];
                    if (asset.Asset == null)
                    {
                        if (convert)
                            cvt.ConvertData(this, assetIndex);
                        else
                            continue;
                    }

                    asset.Asset.Dirty = true;
                    jsonList.Add(new ProjectJson.AssetEntry()
                    {
                        Name = asset.Name,
                        Path = $"{assetDir}/{asset.Name}/{asset.Name}.json"
                    });
                }
            }
            else
            {
                // Use a direct JSON file
                foreach (int assetIndex in indices)
                {
                    AssetRef<T> asset = list[assetIndex];
                    if (asset.Asset == null)
                    {
                        if (convert)
                            cvt.ConvertData(this, assetIndex);
                        else
                            continue;
                    }

                    asset.Asset.Dirty = true;
                    jsonList.Add(new ProjectJson.AssetEntry()
                    {
                        Name = asset.Name,
                        Path = $"{assetDir}/{asset.Name}.json"
                    });
                }
            }
        }

        /// <summary>
        /// Adds all dirty assets of this type in the project to the JSON; essentially marking them to be exported
        /// </summary>
        public void AddDirtyAssetsToJSON<T>(List<AssetRef<T>> list, string assetDir = null) where T : Asset
        {
            string jsonName = ProjectJson.AssetTypeName[typeof(T)];
            assetDir ??= jsonName.ToLowerInvariant();

            List<ProjectJson.AssetEntry> jsonList;
            if (!JsonFile.Assets.TryGetValue(jsonName, out jsonList))
                JsonFile.Assets[jsonName] = jsonList = new List<ProjectJson.AssetEntry>();

            if (ProjectJson.AssetUsesFolder.Contains(typeof(T)))
            {
                // Use a folder, with a JSON inside of it
                foreach (AssetRef<T> asset in list)
                {
                    if (asset.Asset == null || !asset.Asset.Dirty)
                        continue; // Skip if this isn't modified

                    jsonList.Add(new ProjectJson.AssetEntry()
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

                    jsonList.Add(new ProjectJson.AssetEntry()
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
            List<ProjectJson.AssetEntry> entries;
            if (!JsonFile.Assets.TryGetValue(ProjectJson.AssetTypeName[typeof(T)], out entries))
                return; // This asset type doesn't exist in this JSON...

            AssetConverter<T> cvt = null;

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

                    int assetIndex = assetIndices[entry.Name];
                    T asset = list[assetIndex].Asset;
                    if (asset == null)
                    {
                        // Need to convert now
                        if (cvt == null)
                            cvt = (GetConverter(AssetTypeConverter[typeof(T)]) as AssetConverter<T>);
                        cvt.ConvertData(this, assetIndex);
                        asset = list[assetIndex].Asset;
                    }
                    if (asset.Hash == null)
                        asset.ComputeHash(this);
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

    public class ProjectJson
    {
        public struct AssetEntry
        {
            public string Name { get; set; }
            public string Path { get; set; }
        }

        public class AudioGroupSettings
        {
            public List<string> AudioGroups { get; set; } // If null, don't mess with these
            public List<string> NewAudioGroups { get; set; }
        }

        public struct TextureGroupSettings
        {
            public int MaxTextureWidth { get; set; }
            public int MaxTextureHeight { get; set; }
            public List<TextureGroupByID> Groups { get; set; } // If null, don't mess with these
            public List<TextureGroup> NewGroups { get; set; }
        }

        public class TextureGroup
        {
            public string Name { get; set; } // note/todo: when changed, this needs to be updated in actual relevant group class
            public int Border { get; set; }
            public bool AllowCrop { get; set; }
        }

        public class TextureGroupByID : TextureGroup
        {
            public int ID { get; set; } // in ProjectFile.Textures.TextureGroups
        }

        public struct OptionsSettings
        {
            public OptionsFlags Flags { get; set; }
            public int Scale { get; set; }
            public uint WindowColor { get; set; }
            public uint ColorDepth { get; set; }
            public uint Resolution { get; set; }
            public uint Frequency { get; set; }
            public uint VertexSync { get; set; }
            public uint Priority { get; set; }
            public uint LoadAlpha { get; set; }
            public List<Constant> Constants { get; set; }

            public struct Constant
            {
                public string Name { get; set; }
                public string Value { get; set; }
            }
        }

        public int Version { get; private set; } = 1;
        public int BaseFileLength { get; set; } = 0;
        public byte[] BaseFileHash { get; set; } = null;
        public string Info { get; set; } // Filename of info JSON
        public string Options { get; set; } // Filename of options JSON
        public string AudioGroups { get; set; } // Filename of audio group JSON
        public string DataFiles { get; set; } = ""; // Folder name of data files
        public string TextureGroups { get; set; } // Filename of texture group JSON
        public Dictionary<string, List<AssetEntry>> Assets { get; set; }


        public readonly static Dictionary<Type, string> AssetTypeName = new Dictionary<Type, string>()
        {
            { typeof(AssetSound), "Sounds" },
            { typeof(AssetSprite), "Sprites" },
            { typeof(AssetBackground), "Backgrounds" },
            { typeof(AssetFont), "Fonts" },
            { typeof(AssetObject), "Objects" },
            { typeof(AssetPath), "Paths" },
            { typeof(AssetRoom), "Rooms" },
        };
        public readonly static HashSet<Type> AssetUsesFolder = new HashSet<Type>()
        {
            typeof(AssetSound),
            typeof(AssetBackground),
            typeof(AssetSprite),
            typeof(AssetFont),

            // Code entries not implemented yet, will be eventually
            typeof(AssetObject), 
            typeof(AssetRoom)
        };

        public ProjectJson()
        {
            Assets = new Dictionary<string, List<AssetEntry>>();
            foreach (var pair in AssetTypeName)
                Assets.Add(pair.Value, new List<AssetEntry>());
        }
    }
}
