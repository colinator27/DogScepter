using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using DogScepterLib.Core;
using DogScepterLib.Core.Chunks;
using DogScepterLib.Project;
using DogScepterLib.Project.Converters;
using DogScepterLib.Project.GML.Decompiler;
using DogScepterLib.Project.Util;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using DogScepterLib.Core.Models;

namespace DogScepterCLI.Commands;

/// <summary>
/// The "dump" command, which dumps certain information from a GameMaker data file.
/// </summary>
[Command("dump", Description = "Dumps certain information from an input data file path.")]
// ReSharper disable once UnusedType.Global
public class DumpCommand : ICommand
{
    /// <summary>
    /// File path to the GameMaker data file.
    /// </summary>
    [CommandParameter(0, Description = "Input data file path.")]
    public string DataFile { get; private set; } = null;

    /// <summary>
    /// Directory path on where to output dumped files. If <see langword="null"/>, then the current working directory should be used.
    /// </summary>
    [CommandOption("output", 'o', Description = "If not the working directory, specifies the output directory.")]
    public string OutputDirectory { get; private set; } = null;

    /// <summary>
    /// Whether to show verbose output from operations.
    /// </summary>
    [CommandOption("verbose", 'v', Description = "Whether to show verbose output from operations.")]
    public bool Verbose { get; init; } = false;

    /// <summary>
    /// Whether to dump textures.
    /// </summary>
    [CommandOption("textures", Description = "Dump textures.")]
    public bool DumpTextures { get; private set; }

    /// <summary>
    /// Whether to dump strings.
    /// </summary>
    [CommandOption("strings", Description = "Dump strings.")]
    public bool DumpStrings { get; private set; }

    /// <summary>
    /// Whether to dump decompiled code.
    /// </summary>
    [CommandOption("code", Description = "Dump decompiled code.")]
    public bool DumpCode { get; private set; }

    /// <summary>
    /// Whether to dump rooms as JSON.
    /// </summary>
    [CommandOption("rooms", Description = "Dump room JSON.")]
    public bool DumpRooms { get; private set; }


    /// <summary>
    /// Whether to enable features more useful for comparing versions of a game
    /// </summary>
    [CommandOption("hackycompare", Description = "Enables hacky comparison mode.")]
    public bool ComparisonMode { get; private set; }


    /// <summary>
    /// The name of the macro config that should be used.
    /// </summary>
    [CommandOption("config", Description = "Set the configuration to use.")]
    public string Config { get; private set; } = null;

    public ValueTask ExecuteAsync(IConsole console)
    {
        console.Output.WriteLine();

        string dir = OutputDirectory ?? Environment.CurrentDirectory;

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

        // Initialize the project file
        GMData data = console.LoadDataFile(DataFile, Verbose);
        if (data == null)
            return default;
        ProjectFile pf = console.OpenProject(data, dir);
        if (pf == null)
            return default;
        pf.HackyComparisonMode = ComparisonMode;

        // If any dump options were specified, overwrite to true, otherwise false.
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
                    console.Error.WriteLine($"Failed to save texture {i}: {e.Message}");
                }
            }
        }

        if (DumpStrings)
        {
            didAnything = true;
            console.Output.WriteLine("Dumping strings...");

            StringBuilder sb = new StringBuilder();
            foreach (GMString str in pf.DataHandle.GetChunk<GMChunkSTRG>().List)
                sb.AppendLine(str.ToString());
            try
            {
                File.WriteAllText(Path.Combine(dir, "strings.txt"), sb.ToString());
            }
            catch (Exception e)
            {
                console.Error.WriteLine($"Failed to save strings: {e.Message}");
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
                        console.Error.WriteLine($"Didn't find a macro type config named \"{Config}\".");
                }
                catch (Exception ex)
                {
                    console.Error.WriteLine($"Failed to load macro type config: {ex}");
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
                    console.Error.WriteLine($"Failed to decompile code for \"{elem.Name.Content}\": {e}");
                }
            });
        }

        if (DumpRooms)
        {
            didAnything = true;
            console.Output.WriteLine("Dumping rooms...");

            string roomOutputDir = Path.Combine(dir, "rooms");
            Directory.CreateDirectory(roomOutputDir);

            for (int i = 0; i < pf.Rooms.Count; i++)
            {
                try
                {
                    pf.GetConverter<RoomConverter>().ConvertData(pf, i);
                    pf.Rooms[i].Asset.Write(pf, Path.Combine(roomOutputDir, pf.Rooms[i].Name + ".json"));
                }
                catch (Exception e)
                {
                    console.Error.WriteLine($"Failed to export room data for \"{pf.Rooms[i].Name}\": {e}");
                }
            }
        }

        if (didAnything)
            console.Output.WriteLine("Complete.");
        else
            console.Output.WriteLine("Did nothing. Need to specify using parameters what to dump.");

        return default;
    }
}