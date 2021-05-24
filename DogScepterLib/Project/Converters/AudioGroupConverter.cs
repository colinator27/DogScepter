using DogScepterLib.Core;
using DogScepterLib.Core.Chunks;
using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DogScepterLib.Project.Converters
{
    public class AudioGroupConverter : IConverter
    {
        public void ConvertData(ProjectFile pf)
        {
            pf.JsonFile.AudioGroups = "audiogroups.json";

            var agrp = pf.DataHandle.GetChunk<GMChunkAGRP>();
            if (agrp == null)
            {
                // format ID <= 13
                pf.AudioGroups = null;
                return;
            }
            var groups = agrp.List;

            // Make a cached map of group IDs to chunks
            pf._CachedAudioChunks = new Dictionary<int, GMChunkAUDO>()
                { { pf.DataHandle.VersionInfo.BuiltinAudioGroupID, pf.DataHandle.GetChunk<GMChunkAUDO>() } };
            if (agrp.AudioData != null)
            {
                for (int i = 1; i < groups.Count; i++)
                {
                    if (agrp.AudioData.ContainsKey(i))
                        pf._CachedAudioChunks.Add(i, agrp.AudioData[i].GetChunk<GMChunkAUDO>());
                }
            }

            // Actually make the list
            pf.AudioGroups = new List<string>();
            foreach (GMAudioGroup g in groups)
                pf.AudioGroups.Add(g.Name.Content);
        }

        public void ConvertProject(ProjectFile pf)
        {
            GMChunkAGRP groups = pf.DataHandle.GetChunk<GMChunkAGRP>();
            if (groups == null || pf.AudioGroups == null)
                return;

            groups.List.Clear();
            int ind = 0;
            foreach (string g in pf.AudioGroups)
            {
                if (groups.AudioData != null && ind != 0 && !groups.AudioData.ContainsKey(ind))
                {
                    // Well now we have to make a new group file
                    GMData data = new GMData()
                    {
                        Length = 1024 * 1024 // just a random default
                    };
                    data.FORM = new GMChunkFORM()
                    {
                        ChunkNames = new List<string>() { "AUDO" },
                        Chunks = new Dictionary<string, GMChunk>()
                        {
                            { "AUDO", new GMChunkAUDO() { List = new GMPointerList<GMAudio>() } }
                        }
                    };
                    groups.AudioData[ind] = data;
                }

                groups.List.Add(new GMAudioGroup()
                {
                    Name = pf.DataHandle.DefineString(g)
                });

                ind++;
            }
        }
    }
}
