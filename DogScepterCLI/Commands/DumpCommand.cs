using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using DogScepterLib.Core;
using DogScepterLib.Core.Chunks;
using DogScepterLib.Project;
using DogScepterLib.Project.GML.Decompiler;
using DogScepterLib.Project.Util;
using DogScepterLib.User;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DogScepterCLI.Commands
{
    [Command("dump", Description = "Dumps certain information from an input data file path.")]
    public class DumpCommand : ICommand
    {
        [CommandParameter(0, Description = "Input data file path.")]
        public string DataFile { get; private set; } = null;

        [CommandOption("output", 'o', Description = "If not the working directory, specifies the output directory.")]
        public string OutputDirectory { get; private set; } = null;

        [CommandOption("verbose", 'v', Description = "Whether to show verbose output from operations.")]
        public bool Verbose { get; init; } = false;

        [CommandOption("textures", Description = "Dump textures.")]
        public bool DumpTextures { get; private set; }

        [CommandOption("strings", Description = "Dump strings.")]
        public bool DumpStrings { get; private set; }

        [CommandOption("code", Description = "Dump decompiled code.")]
        public bool DumpCode { get; private set; }

        [CommandOption("config", Description = "Set the configuration to use.")]
        public string Config { get; private set; } = null;

        public ValueTask ExecuteAsync(IConsole console)
        {
            console.Output.WriteLine();

            string dir = OutputDirectory ?? Environment.CurrentDirectory;

            // Initialize the project file
            GMData data = console.LoadDataFile(DataFile, Verbose);
            if (data == null)
                return default;
            ProjectFile pf = console.OpenProject(data, dir);
            if (data == null)
                return default;

            if (!Directory.Exists(dir))
            {
                if (console.PromptYesNo($"Directory \"{dir}\" does not exist. Create it?"))
                    Directory.CreateDirectory(dir);
                else
                {
                    console.Output.WriteLine("Bailing.");
                    return default;
                }
            }

            bool didAnything = false;

            if (DumpTextures)
            {
                didAnything = true;
                console.Output.WriteLine("Dumping textures...");
                pf.Textures.ParseAllTextures();
                for (int i = 0; i < pf.Textures.CachedTextures.Length; i++)
                {
                    DSImage curr = pf.Textures.CachedTextures[i];
                    if (curr == null)
                        continue;
                    try
                    {
                        curr.SavePng(Path.Combine(dir, $"texture_{i}.png"));
                    }
                    catch (Exception e)
                    {
                        console.Output.WriteLine($"Failed to save texture {i}: {e.Message}");
                    }
                }
            }

            if (DumpStrings)
            {
                didAnything = true;
                console.Output.WriteLine("Dumping strings...");

                StringBuilder sb = new StringBuilder();
                foreach (var str in pf.DataHandle.GetChunk<GMChunkSTRG>().List)
                    sb.AppendLine(str.ToString());
                try
                {
                    File.WriteAllText(Path.Combine(dir, "strings.txt"), sb.ToString());
                }
                catch (Exception e)
                {
                    console.Output.WriteLine($"Failed to save strings: {e.Message}");
                }
            }

            if (DumpCode)
            {
                didAnything = true;
                console.Output.WriteLine("Dumping code...");

                pf.DecompileCache = new DecompileCache(pf);

                if (Config != null)
                {
                    try
                    {
                        if (!pf.DecompileCache.Types.AddFromConfigFile(Config))
                            console.Output.WriteLine($"Didn't find a macro type config named \"{Config}\".");
                    }
                    catch (Exception ex)
                    {
                        console.Output.WriteLine($"Failed to load macro type config: {ex}");
                    }
                }

                string codeOutputDir = Path.Combine(dir, "code");
                Directory.CreateDirectory(codeOutputDir);

                var codeList = pf.DataHandle.GetChunk<GMChunkCODE>().List;
                Parallel.ForEach(codeList, elem =>
                {
                    if (elem.ParentEntry != null)
                        return;
                    try
                    {
                        File.WriteAllText(Path.Combine(codeOutputDir, elem.Name.Content[0..Math.Min(elem.Name.Content.Length, 128)] + ".gml"),
                                          new DecompileContext(pf).DecompileWholeEntryString(elem));
                    }
                    catch (Exception e)
                    {
                        console.Output.WriteLine($"Failed to decompile code for \"{elem.Name.Content}\": {e}");
                    }
                });
            }

            if (didAnything)
            {
                console.Output.WriteLine("Complete.");
            }
            else
            {
                console.Output.WriteLine("Did nothing. Need to specify using parameters what to dump.");
            }

            return default;
        }
    }
}
