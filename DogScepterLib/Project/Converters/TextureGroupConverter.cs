using DogScepterLib.Core.Chunks;
using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DogScepterLib.Project.Converters
{
    public class TextureGroupConverter : IConverter
    {
        public void ConvertData(ProjectFile pf)
        {
            pf.JsonFile.TextureGroups = "texturegroups.json";

            var settings = new ProjectJson.TextureGroupSettings()
            {
                MaxTextureWidth = 2048,
                MaxTextureHeight = 2048
            };
            var settingsList = new List<ProjectJson.TextureGroupByID>();
            settings.Groups = settingsList;
            settings.NewGroups = new List<ProjectJson.TextureGroup>();
            var tgin = pf.DataHandle.GetChunk<GMChunkTGIN>();
            List<Textures.Group> list;
            if (pf.InternalTextures == null)
            {
                pf.InternalTextures = new Textures(pf, true);
                list = pf.InternalTextures.TextureGroups; // Don't do all the processing if not needed
            }
            else
                list = pf.Textures.TextureGroups;

            if (tgin != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var group = list[i];
                    GMTextureGroupInfo resultInfo = null;
                    foreach (var info in tgin.List)
                    {
                        if (info.TexturePageIDs.Any(j => group.Pages.Contains(j.ID)))
                        {
                            resultInfo = info;
                            break;
                        }
                    }
                    var newEntry = new ProjectJson.TextureGroupByID()
                    {
                        Name = resultInfo?.Name?.Content ?? $"unknown_group_{i}",
                        Border = group.Border,
                        AllowCrop = group.AllowCrop,
                        ID = i
                    };
                    group.Name = newEntry.Name;
                    settingsList.Add(newEntry);
                }
            }
            else
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var group = list[i];
                    var newEntry = new ProjectJson.TextureGroupByID()
                    {
                        Name = $"group{i}",
                        Border = group.Border,
                        AllowCrop = group.AllowCrop,
                        ID = i
                    };
                    group.Name = newEntry.Name;
                    settingsList.Add(newEntry);
                }
            }

            pf.TextureGroupSettings = settings;
        }

        public void ConvertProject(ProjectFile pf)
        {
            if (pf.TextureGroupSettings.Groups != null)
            {
                // Sort the texture pages by their ID
                List<int> newGroupIDs = new List<int>();
                Dictionary<int, ProjectJson.TextureGroup> newGroups = new();

                int highest = -1;
                foreach (var group in pf.TextureGroupSettings.Groups)
                {
                    int thisId = group.ID;

                    if (newGroupIDs.Contains(thisId))
                    {
                        // Duplicate ID? Go one above the highest instead
                        pf.DataHandle.Logger?.Invoke($"Warning: overlapping texture group ID {thisId}; changing it automatically.");
                        thisId = highest + 1;
                    }
                    newGroupIDs.Add(thisId);

                    if (thisId > highest)
                        highest = thisId;

                    newGroups[thisId] = group;
                }
                newGroupIDs.Sort();

                // Add new pages to the end
                int i;
                for (i = newGroups.Count - 1; i >= pf.Textures.TextureGroups.Count; i--)
                {
                    var groupInfo = newGroups[newGroupIDs[i]];
                    pf.Textures.TextureGroups.Add(new Textures.Group()
                    {
                        Dirty = true,
                        Border = groupInfo.Border,
                        AllowCrop = groupInfo.AllowCrop,
                        Name = groupInfo.Name
                    });
                }

                // Handle changing properties on other pages
                for (; i >= 0; i--)
                {
                    var groupInfo = newGroups[newGroupIDs[i]];
                    var group = pf.Textures.TextureGroups[i];
                    if (group.Border != groupInfo.Border || group.AllowCrop != groupInfo.AllowCrop)
                    {
                        group.Dirty = true;
                        group.Border = groupInfo.Border;
                        group.AllowCrop = groupInfo.AllowCrop;
                    }
                    group.Name = groupInfo.Name;
                }
            }

            if (pf.TextureGroupSettings.NewGroups != null)
            { 
                foreach (var group in pf.TextureGroupSettings.NewGroups)
                {
                    pf.Textures.TextureGroups.Add(new Textures.Group()
                    {
                        Dirty = true,
                        Border = group.Border,
                        AllowCrop = group.AllowCrop,
                        Name = group.Name
                    });
                }
            }
        }
    }
}
