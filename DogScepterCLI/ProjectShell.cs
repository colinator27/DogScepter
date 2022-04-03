using CliFx.Infrastructure;
using DogScepterLib.Core;
using DogScepterLib.Project;
using DogScepterLib.User;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DogScepterCLI;

public static class ProjectShell
{
    private class Command
    {
        public string[] Names;
        public string Description;
        public string Usage;
        public Func<string[], CommandResult> Function;

        public enum CommandResult
        {
            None,
            InvalidSyntax,
            Quit
        }

        public Command(string[] names, string description, string usage, Func<string[], CommandResult> function)
        {
            Names = names;
            Description = description;
            Usage = usage;
            Function = function;
        }
    }

    private static void AddAsset<T>(IConsole console, string assets, AssetRefList<T> list, ProjectFile pf) where T : Asset
    {
        string[] assetSplit = assets.Split(',');
        List<int> indices = new List<int>();
        foreach (string asset in assetSplit)
        {
            int ind = list.FindIndex(asset);
            if (ind == -1)
            {
                console.Output.WriteLine($"Asset with name \"{asset}\" does not exist; ignoring.");
            }
            else
            {
                if (list[ind].Asset != null)
                {
                    if (!console.PromptYesNo($"Asset \"{asset}\" is already in project. Overwrite?"))
                        continue;
                }
                indices.Add(ind);
            }
        }

        pf.AddAssetsToJSON(list, indices, true);
        pf.SaveAssets(list);
        pf.SaveMain();

        console.Output.WriteLine($"Added {indices.Count} assets.");
    }

    private static bool ReloadProject(IConsole console, ref ProjectFile pf, ProjectConfig cfg, bool verbose, bool reloadData = true)
    {
        // Reload project/data file completely now
        console.Output.WriteLine("Reloading project...");
        GMData data;
        if (reloadData)
        {
            data = console.LoadDataFile(cfg.InputFile, verbose);
            if (data == null)
                return false;
        }
        else
            data = pf.DataHandle;
        pf = console.OpenProject(data, pf.DirectoryPath);
        if (pf == null)
            return false;

        try
        {
            pf.LoadAll();
        }
        catch (Exception e)
        {
            console.Error.WriteLine($"Failed to reload project: {e}");
            return false;
        }

        console.Output.WriteLine("Finished reload.");
        return true;
    }

    public static void Run(IConsole console, ProjectFile pf, ProjectConfig cfg, bool verbose)
    {
        try
        {
            pf.LoadAll();
        }
        catch (Exception e)
        {
            console.Error.WriteLine($"Failed to load project: {e}");
            return;
        }

        List<Command> commands = new List<Command>()
        {
            new Command(new[] { "exit", "quit" },
                "Exits this shell.",
                "[exit|quit]",
                args =>
                {
                    // TODO prompt to save anything? maybe not?
                    return Command.CommandResult.Quit;
                }),

            //TODO: don't just straight up crash, if project file doesn't exist anymore, needs to be fixed in OpenProject
            new Command(new[] { "reload" },
                "Reloads the project as it currently is on disk.",
                "reload <optional:data>",
                args =>
                {
                    if (!ReloadProject(console, ref pf, cfg, verbose, (args.Length == 2 && args[1] == "data")))
                        return Command.CommandResult.Quit;
                    return Command.CommandResult.None;
                }),

            new Command(new[] { "add" },
                "Adds an asset from game data to the project.",
                "add <asset_type> <asset_names>",
                args =>
                {
                    if (args.Length != 3)
                        return Command.CommandResult.InvalidSyntax;
                    switch (args[1].ToLowerInvariant())
                    {
                        case "path": case "paths":
                            AddAsset(console, args[2], pf.Paths, pf);
                            break;
                        case "sprite": case "sprites":
                            AddAsset(console, args[2], pf.Sprites, pf);
                            break;
                        case "sound": case "sounds":
                            AddAsset(console, args[2], pf.Sounds, pf);
                            break;
                        case "object": case "objects":
                            AddAsset(console, args[2], pf.Objects, pf);
                            break;
                        case "background": case "backgrounds":
                            AddAsset(console, args[2], pf.Backgrounds, pf);
                            break;
                        case "font": case "fonts":
                            AddAsset(console, args[2], pf.Fonts, pf);
                            break;
                        case "room": case "rooms":
                            AddAsset(console, args[2], pf.Rooms, pf);
                            break;
                        default:
                            return Command.CommandResult.InvalidSyntax;
                    }
                    return Command.CommandResult.None;
                }),

            new Command(new[] { "apply" },
                "Applies the project to the input data file, resulting in output.",
                "apply",
                args =>
                {
                    try
                    {
                        pf.LoadAll();

                        using FileStream fs = new FileStream(Path.Combine(cfg.OutputDirectory, pf.DataHandle.Filename), FileMode.Create);
                        using GMDataWriter writer = new GMDataWriter(pf.DataHandle, fs, fs.Name, pf.DataHandle.Length);

                        console.Output.WriteLine("Converting to data...");
                        pf.ConvertToData();
                        console.Output.WriteLine("Writing main data file...");
                        writer.Write();
                        writer.Flush();
                        foreach (GMWarning w in writer.Warnings)
                            console.Output.WriteLine($"[WARN: {w.Level}] {w.Message}"); // todo formatting
                    }
                    catch (Exception e)
                    {
                        console.Error.WriteLine($"Failed to apply project: {e}");
                    }

                    if (!ReloadProject(console, ref pf, cfg, verbose))
                        return Command.CommandResult.Quit;

                    return Command.CommandResult.None;
                }),

            new Command(new[] { "about" },
                "Displays information about the open project.",
                "about",
                args =>
                {
                    console.Output.Write("Data file location: ");
                    console.Output.WriteLine(cfg.InputFile);
                    console.Output.Write("Output directory: ");
                    console.Output.WriteLine(cfg.OutputDirectory);
                    return Command.CommandResult.None;
                })
        };
        int helpLength = commands.Max(c => c.Usage.Length) + 1;

        console.Error.WriteLine();
        console.Error.WriteLine("DogScepter project shell");

        bool running = true;
        while (running)
        {
            console.Output.Write("> ");
            string command = console.Input.ReadLine();
            string[] args = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (args.Length >= 1)
            {
                string name = args[0].ToLowerInvariant();

                if (name == "help")
                {
                    if (args.Length == 2)
                    {
                        string helpName = args[1].ToLowerInvariant();
                        Command cmd = commands.Find(c => c.Names.Contains(helpName));
                        if (cmd != null)
                        {
                            console.Output.WriteLine(cmd.Description);
                            console.Output.WriteLine(cmd.Usage);
                            console.Output.WriteLine();
                            continue;
                        }
                    }

                    foreach (var cmd in commands)
                    {
                        console.Output.WriteLine(cmd.Usage + new string(' ', helpLength - cmd.Usage.Length) +
                                                 " |  " + cmd.Description);
                    }
                }
                else
                {
                    Command cmd = commands.Find(c => c.Names.Contains(name));
                    if (cmd == null)
                        console.Error.WriteLine($"Unknown command \"{args[0]}\"");
                    else
                    {
                        switch (cmd.Function(args))
                        {
                            case Command.CommandResult.InvalidSyntax:
                                console.Error.WriteLine("Invalid syntax; proper usage:");
                                console.Error.WriteLine("  " + cmd.Usage);
                                break;
                            case Command.CommandResult.Quit:
                                running = false;
                                break;
                        }
                    }
                }
            }

            console.Output.WriteLine();
        }
    }
}