using DogScepterLib.Core.Chunks;
using DogScepterLib.Core.Models;
using DogScepterLib.Project.Assets;
using DogScepterLib.Project.GML.Compiler;
using DogScepterLib.Project.GML.Decompiler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DogScepterLib.Project.Converters
{
    public class CodeConverter : AssetConverter<AssetCode>
    {
        public override void ConvertData(ProjectFile pf, int index)
        {
            GMCode asset = (GMCode)pf.Code[index].DataAsset;

            string name = asset.Name.Content;
            bool isScript = (name.StartsWith("gml_Script") || name.StartsWith("gml_GlobalScript"));

            ConvertData(pf, index, asset, isScript);
        }

        public void ConvertData(ProjectFile pf, int index, GMCode dataAsset, bool isScript)
        {
            AssetCode projectAsset = new AssetCode()
            {
                Name = dataAsset.Name.Content,
                IsScript = isScript,
                Patches = new()
            };
            
            if (isScript && pf.DataHandle.VersionInfo.IsVersionAtLeast(2, 3) &&
                projectAsset.Name.StartsWith("gml_GlobalScript_"))
            {
                // If this entry's name starts with "gml_GlobalScript_", and this is a script, we need to remove it
                projectAsset.Name = projectAsset.Name["gml_GlobalScript_".Length..];
            }

            string codeString;
            CodeContext.CodeMode mode;
            try
            {
                pf.DecompileCache ??= new DecompileCache(pf);
                codeString = new DecompileContext(pf).DecompileWholeEntryString(dataAsset);
                mode = CodeContext.CodeMode.Replace;
            }
            catch (Exception e)
            {
                // In order to not cause problems, insert blank code at the end of the entry
                codeString = $"/*\n\nCode decompilation failed for this entry!\n\n{e}\n\n*/";
                mode = CodeContext.CodeMode.InsertEnd;
            }

            projectAsset.Patches.Add(new()
            {
                Code = codeString,
                Mode = mode,
                Filename = $"{dataAsset.Name.Content[0..Math.Min(dataAsset.Name.Content.Length, 100)]}.gml"
            });

            pf.Code[index].Asset = projectAsset;
        }

        public override void ConvertData(ProjectFile pf)
        {
            var dataCode = pf.DataHandle.GetChunk<GMChunkCODE>()?.List;
            if (dataCode == null)
                return;
            for (int i = 0; i < dataCode.Count; i++)
            {
                GMCode code = dataCode[i];
                if (code.ParentEntry != null)
                    continue;
                pf.Code.Add(new AssetRef<AssetCode>(code.Name.Content, i, code));
            }
        }

        public override void ConvertProject(ProjectFile pf)
        {
            var dataAssets = pf.DataHandle.GetChunk<GMChunkCODE>()?.List;
            if (dataAssets == null)
                return;

            CompileContext ctx = null; 

            for (int i = 0; i < pf.Code.Count; i++)
            {
                // Get project-level asset
                AssetCode assetCode = pf.Code[i].Asset;
                if (assetCode == null)
                {
                    // This asset was never converted
                    continue;
                }
                
                // Add compilation context for this code
                ctx ??= new CompileContext(pf);
                foreach (var patch in assetCode.Patches)
                    ctx.AddCode(assetCode.Name, patch.Code, patch.Mode, assetCode.IsScript);
            }

            if (ctx != null)
            {
                pf.DataHandle.Logger?.Invoke($"Compiling {ctx.Code.Count} GML entries...");
                if (ctx.Compile())
                {
                    pf.DataHandle.Logger?.Invoke("Compilation successful.");
                }
                else
                {
                    pf.WarningHandler.Invoke(ProjectFile.WarningType.CodeCompilationFailure, "GML code compilation was unsuccessful.");
                    pf.DataHandle.Logger?.Invoke($"Listing {ctx.Errors.Count} compile errors:");
                    foreach (var err in ctx.Errors)
                    {
                        pf.DataHandle.Logger?.Invoke($"[{err.Context.Name}:{err.Line}:{err.Column}] {err.Message}");
                    }
                }
            }
        }
    }
}
