using DogScepterLib.Core;
using DogScepterLib.Core.Chunks;
using DogScepterLib.Core.Models;
using DogScepterLib.Project.Assets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static DogScepterLib.Core.Models.GMSound;

namespace DogScepterLib.Project.Converters
{
    public class SoundConverter : AssetConverter<AssetSound>
    {
        public class CachedSoundRefData : CachedRefData
        {
            public BufferRegion SoundBuffer { get; set; }
            public string AudioGroupName { get; set; }

            public CachedSoundRefData(BufferRegion soundBuffer, string audioGroupName)
            {
                SoundBuffer = soundBuffer;
                AudioGroupName = audioGroupName;
            }
        }

        public override void ConvertData(ProjectFile pf, int index)
        {
            GMSound asset = (GMSound)pf.Sounds[index].DataAsset;

            AssetSound projectAsset = new AssetSound()
            {
                Name = asset.Name?.Content,
                AudioGroup = ((CachedSoundRefData)pf.Sounds[index].CachedData).AudioGroupName,
                Volume = asset.Volume,
                Pitch = asset.Pitch,
                Type = asset.Type?.Content,
                OriginalSoundFile = asset.File.Content,
                SoundFile = asset.File.Content
            };

            if ((asset.Flags & AudioEntryFlags.IsEmbedded) != AudioEntryFlags.IsEmbedded &&
                (asset.Flags & AudioEntryFlags.IsCompressed) != AudioEntryFlags.IsCompressed)
            {
                // External file
                projectAsset.Attributes = AssetSound.Attribute.CompressedStreamed;

                string soundFilePath = Path.Combine(pf.DataHandle.Directory, asset.File.Content);
                if (!soundFilePath.EndsWith(".ogg") && !soundFilePath.EndsWith(".mp3"))
                    soundFilePath += ".ogg";

                if (File.Exists(soundFilePath))
                {
                    projectAsset.SoundFileBuffer = new BufferRegion(File.ReadAllBytes(soundFilePath));
                    if (!projectAsset.SoundFile.Contains("."))
                        projectAsset.SoundFile += Path.GetExtension(soundFilePath);
                }
            }
            else
            {
                // Internal file
                projectAsset.SoundFileBuffer = pf._CachedAudioChunks[asset.GroupID].List[asset.AudioID].Data;

                if ((asset.Flags & AudioEntryFlags.IsCompressed) == AudioEntryFlags.IsCompressed)
                {
                    // But compressed!
                    if ((asset.Flags & AudioEntryFlags.IsEmbedded) == AudioEntryFlags.IsEmbedded)
                        projectAsset.Attributes = AssetSound.Attribute.UncompressOnLoad;
                    else
                        projectAsset.Attributes = AssetSound.Attribute.CompressedNotStreamed;
                    if (projectAsset.SoundFileBuffer.Length > 4 && !projectAsset.SoundFile.Contains("."))
                    {
                        var span = projectAsset.SoundFileBuffer.Memory.Span;
                        if (span[0] == 'O' && span[1] == 'g' && span[2] == 'g' && span[3] == 'S')
                            projectAsset.SoundFile += ".ogg";
                        else
                            projectAsset.SoundFile += ".mp3";
                    }
                }
                else
                {
                    projectAsset.Attributes = AssetSound.Attribute.Uncompressed;
                    if (!projectAsset.SoundFile.Contains("."))
                        projectAsset.SoundFile += ".wav";
                }
            }

            pf.Sounds[index].Asset = projectAsset;
        }

        public override void ConvertData(ProjectFile pf)
        {
            EmptyRefsForNamed(pf.DataHandle.GetChunk<GMChunkSOND>().List, pf.Sounds, (asset) =>
            {
                GMSound sound = (GMSound)asset;

                BufferRegion buff;
                if ((sound.Flags & AudioEntryFlags.IsEmbedded) != AudioEntryFlags.IsEmbedded &&
                    (sound.Flags & AudioEntryFlags.IsCompressed) != AudioEntryFlags.IsCompressed)
                {
                    buff = null;
                }
                else if (sound.AudioID == -1)
                {
                    // No sound bound?
                    buff = null;
                }
                else
                {
                    if (pf._CachedAudioChunks == null)
                        buff = pf.DataHandle.GetChunk<GMChunkAUDO>().List[sound.AudioID].Data; // legacy versions don't have audio groups
                    else if (pf._CachedAudioChunks.TryGetValue(sound.GroupID, out var chunk))
                        buff = chunk.List[sound.AudioID].Data;
                    else
                    {
                        pf.WarningHandler.Invoke(ProjectFile.WarningType.MissingAudioGroup, $"Missing audio group ID {sound.GroupID} for {sound.Name?.Content ?? "<null>"}");
                        buff = null;
                    }
                }

                if (pf.AudioGroupSettings == null)
                    return new CachedSoundRefData(buff, "");

                return new CachedSoundRefData(buff,
                                                (sound.GroupID >= 0 && sound.GroupID < pf.AudioGroupSettings.AudioGroups.Count)
                                                    ? pf.AudioGroupSettings.AudioGroups[sound.GroupID] : "");
            });
        }

        public override void ConvertProject(ProjectFile pf)
        {
            var dataAssets = pf.DataHandle.GetChunk<GMChunkSOND>().List;
            var agrp = pf.DataHandle.GetChunk<GMChunkAGRP>();
            var groups = agrp?.List;

            bool updatedVersion = pf.DataHandle.VersionInfo.IsNumberAtLeast(1, 0, 0, 9999);

            // First, sort sounds alphabetically
            List<AssetRef<AssetSound>> sortedSounds = updatedVersion ? pf.Sounds.OrderBy(x => x.Name).ToList() : pf.Sounds;

            // Get all the AUDO chunk handles in the game
            GMChunkAUDO defaultChunk = pf.DataHandle.GetChunk<GMChunkAUDO>();
            defaultChunk.List.Clear();
            Dictionary<string, GMChunkAUDO> audioChunks = new Dictionary<string, GMChunkAUDO>();
            Dictionary<string, int> audioChunkIndices = new Dictionary<string, int>();
            if (agrp?.AudioData != null)
            {
                for (int i = 1; i < groups.Count; i++)
                {
                    if (agrp.AudioData.ContainsKey(i))
                    {
                        var currChunk = agrp.AudioData[i].GetChunk<GMChunkAUDO>();
                        currChunk.List.Clear();
                        audioChunks.Add(groups[i].Name.Content, currChunk);
                        audioChunkIndices.Add(groups[i].Name.Content, i);
                    }
                }
            }

            dataAssets.Clear();
            Dictionary<AssetRef<AssetSound>, GMSound> finalMap = new Dictionary<AssetRef<AssetSound>, GMSound>();
            for (int i = 0; i < sortedSounds.Count; i++)
            {
                AssetSound asset = sortedSounds[i].Asset;
                if (asset == null)
                {
                    // This asset was never converted, so handle references and re-add it
                    GMSound s = (GMSound)sortedSounds[i].DataAsset;

                    // Get the group name from the cache
                    var cachedData = (CachedSoundRefData)sortedSounds[i].CachedData;

                    // Potentially handle the internal sound buffer
                    if (cachedData.SoundBuffer != null)
                    {
                        string groupName = cachedData.AudioGroupName;

                        int ind;
                        GMChunkAUDO chunk;
                        if (!audioChunkIndices.TryGetValue(groupName, out ind))
                        {
                            ind = pf.DataHandle.VersionInfo.BuiltinAudioGroupID; // might be wrong
                            chunk = defaultChunk;
                        }
                        else
                            chunk = audioChunks[groupName];

                        s.GroupID = ind;
                        s.AudioID = chunk.List.Count;
                        chunk.List.Add(new GMAudio() { Data = cachedData.SoundBuffer });
                    }

                    finalMap[sortedSounds[i]] = s;
                    continue;
                }

                GMSound dataAsset = new GMSound()
                {
                    Name = pf.DataHandle.DefineString(asset.Name),
                    Volume = asset.Volume,
                    Flags = GMSound.AudioEntryFlags.Regular,
                    Effects = 0,
                    Pitch = asset.Pitch,
                    File = pf.DataHandle.DefineString(asset.OriginalSoundFile),
                    Type = (asset.Type != null) ? pf.DataHandle.DefineString(asset.Type) : null
                };
                finalMap[sortedSounds[i]] = dataAsset;

                switch (asset.Attributes)
                {
                    case AssetSound.Attribute.CompressedStreamed:
                        if (updatedVersion)
                            dataAsset.AudioID = -1;
                        else
                            dataAsset.AudioID = defaultChunk.List.Count - 1;
                        dataAsset.GroupID = pf.DataHandle.VersionInfo.BuiltinAudioGroupID; // might be wrong

                        if (asset.SoundFileBuffer != null)
                        {
                            pf.DataHandle.Logger?.Invoke($"Writing sound file \"{asset.SoundFile}\"...");
                            pf.DataHandle.FileWrites.Post(new (Path.Combine(pf.DataHandle.Directory, asset.SoundFile), asset.SoundFileBuffer));
                        }
                        break;
                    case AssetSound.Attribute.UncompressOnLoad:
                    case AssetSound.Attribute.Uncompressed:
                        dataAsset.Flags |= GMSound.AudioEntryFlags.IsEmbedded;
                        goto case AssetSound.Attribute.CompressedNotStreamed;
                    case AssetSound.Attribute.CompressedNotStreamed:
                        if (asset.Attributes != AssetSound.Attribute.Uncompressed)
                            dataAsset.Flags |= GMSound.AudioEntryFlags.IsCompressed;

                        int ind;
                        GMChunkAUDO chunk;
                        if (!audioChunkIndices.TryGetValue(asset.AudioGroup, out ind))
                        {
                            ind = pf.DataHandle.VersionInfo.BuiltinAudioGroupID; // might be wrong
                            chunk = defaultChunk;
                        }
                        else
                            chunk = audioChunks[asset.AudioGroup];

                        dataAsset.GroupID = ind;
                        dataAsset.AudioID = chunk.List.Count;
                        chunk.List.Add(new GMAudio() { Data = asset.SoundFileBuffer });
                        break;
                }
            }

            // Actually add sounds to the data
            foreach (var assetRef in pf.Sounds)
            {
                dataAssets.Add(finalMap[assetRef]);
            }
        }
    }
}
