using DogScepterLib.Core.Chunks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static DogScepterLib.Core.Chunks.GMChunkOPTN;

namespace DogScepterLib.Project.Converters
{
    public class OptionsConverter : IConverter
    {
        public void ConvertData(ProjectFile pf)
        {
            pf.JsonFile.Options = "options.json";

            var optn = pf.DataHandle.GetChunk<GMChunkOPTN>();

            var res = new ProjectJson.OptionsSettings()
            {
                Flags = optn.Options,
                Scale = optn.Scale,
                WindowColor = optn.WindowColor,
                ColorDepth = optn.ColorDepth,
                Resolution = optn.Resolution,
                Frequency = optn.Frequency,
                VertexSync = optn.VertexSync,
                Priority = optn.Priority,
                LoadAlpha = optn.LoadAlpha,
                Constants = new List<ProjectJson.OptionsSettings.Constant>()
            };

            foreach (var constant in optn.Constants)
            {
                res.Constants.Add(new ProjectJson.OptionsSettings.Constant()
                {
                    Name = constant.Name.Content,
                    Value = constant.Value.Content
                });
            }

            pf.Options = res;
        }

        public void ConvertProject(ProjectFile pf)
        {
            var optn = pf.DataHandle.GetChunk<GMChunkOPTN>();

            optn.Options = pf.Options.Flags;
            optn.Scale = pf.Options.Scale;
            optn.WindowColor = pf.Options.WindowColor;
            optn.ColorDepth = pf.Options.ColorDepth;
            optn.Resolution = pf.Options.Resolution;
            optn.Frequency = pf.Options.Frequency;
            optn.VertexSync = pf.Options.VertexSync;
            optn.Priority = pf.Options.Priority;
            optn.LoadAlpha = pf.Options.LoadAlpha;

            optn.Constants.Clear();
            foreach (var constant in pf.Options.Constants)
            {
                optn.Constants.Add(new Constant()
                {
                    Name = pf.DataHandle.DefineString(constant.Name),
                    Value = pf.DataHandle.DefineString(constant.Value)
                });
            }
        }
    }
}
