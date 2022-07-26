using CliFx.Infrastructure;
using DogScepterLib.Core;
using DogScepterLib.Project;
using DogScepterLib.User;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DogScepterCLI;

/// <summary>
/// A small shell allowing you to interact with a DogScepter Project.
/// </summary>
public static class ProjectShell
{
    /// <summary>
    /// Available commands for the shell.
    /// </summary>
    private class Command
    {
        /// <summary>
        /// The names of the command through which it can be invoked.
        /// </summary>
        public readonly string[] Names;

        /// <summary>
        /// A description of the command.
        /// </summary>
        public readonly string Description;

        /// <summary>
        /// A small showcase on how the command is able to be used. <br/>
        /// I.e <c>"add &lt;asset_type&gt; &lt;asset_names&gt;"</c>.
        /// </summary>
        public readonly string Usage;

        /// <summary>
        /// The function that this command executes when invoked.
        /// </summary>
        public readonly Func<string[], CommandResult> Function;

        /// <summary>
        /// Possible results from an executed command-
        /// </summary>
        public enum CommandResult
        {
            /// <summary>
            /// No specified result.
            /// </summary>
            None,

            /// <summary>
            /// Command was invoked with the wrong syntax.
            /// </summary>
            InvalidSyntax,

            /// <summary>
            /// Notices the shell that it should be quit out of.
            /// </summary>
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

    /// <summary>
    /// Starts the shell, allowing you to interact with a DogScepter project until you quit out of it.
    /// </summary>
    /// <param name="console">The console to write output and error messages to, as well as get input from.</param>
    /// <param name="projectFile">The <see cref="ProjectFile"/> that should be interacted with the shell.</param>
    /// <param name="projectConfig">The <see cref="ProjectConfig"/> of the project.</param>
    /// <param name="verbose">Whether to show verbose output.</param>
    public static void Run(IConsole console, ProjectFile projectFile, ProjectConfig projectConfig, bool verbose)
    {
        try
        {
            projectFile.LoadAll();
        }
        catch (Exception e)
        {
            console.Error.WriteLine($"Failed to load project: {e}");
            return;
        }

        List<Command> commands = new List<Command>
        {
            new Command(new[] { "exit", "quit" },
                "Exits this shell.",
                "[exit|quit]",
                _ => ExitCommand()),

            new Command(new[] { "reload" },
                "Reloads the project as it currently is on disk.",
                "reload <optional:data>",
                args => ReloadCommand(console, ref projectFile, projectConfig, verbose, args.Length == 2 && args[1] == "data")),

            new Command(new[] { "add" },
                "Adds an asset from game data to the project.",
                "add <asset_type> <asset_names>",
                args => AddCommand(console, projectFile, args)),

            new Command(new[] { "apply" },
                "Applies the project to the input data file, resulting in output.",
                "apply",
                _ => ApplyCommand(console, ref projectFile, projectConfig, verbose)),

            new Command(new[] { "about" },
                "Displays information about the open project.",
                "about",
                _ => AboutCommand(console, projectConfig))
        };

        int helpLength = commands.Max(c => c.Usage.Length) + 1;

        console.Output.WriteLine();
        console.Output.WriteLine("DogScepter project shell");

        // Enter loop of the shell, until user inputs a command that exits.
        bool running = true;
        while (running)
        {
            console.Output.Write("> ");
            string input = console.Input.ReadLine();
            // User likely entered null on purpose, so we let them quit.
            if (input == null)
            {
                console.Output.WriteLine("Quitting...");
                break;
            }

            string[] args = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (args.Length < 1)
            {
                console.Output.WriteLine();
                continue;
            }

            string name = args[0].ToLowerInvariant();

            // Special case if user wants help displayed.
            if (name == "help")
            {
                // If two words were inputted, print help for the specific command.
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

                // Otherwise, just print help for every command.
                foreach (Command cmd in commands)
                    console.Output.WriteLine(cmd.Usage + new string(' ', helpLength - cmd.Usage.Length) + " |  " + cmd.Description);

                console.Output.WriteLine();
                continue;
            }

            // Get the command to run; if it can't be found show error.
            Command command = commands.Find(c => c.Names.Contains(name));
            if (command == null)
            {
                console.Error.WriteLine($"Unknown command \"{name}\"");
                console.Output.WriteLine();
                continue;
            }

            // Run command and act accordingly.
            switch (command.Function(args))
            {
                case Command.CommandResult.InvalidSyntax:
                    console.Error.WriteLine("Invalid syntax; proper usage:");
                    console.Error.WriteLine("  " + command.Usage);
                    break;
                case Command.CommandResult.Quit:
                    console.Output.WriteLine("Quitting...");
                    running = false;
                    break;
            }
            console.Output.WriteLine();
        }
    }

    #region Project Shell Commands

    /// <summary>
    /// The quit command of the project shell.
    /// </summary>
    /// <returns>TODO: currently only quits. Should probably prompt to save as well.</returns>
    private static Command.CommandResult ExitCommand()
    {
        // TODO prompt to save anything? maybe not?
        return Command.CommandResult.Quit;
    }

    /// <summary>
    /// The reload command of the project shell.
    /// </summary>
    /// <param name="console">The console on where to output messages to.</param>
    /// <param name="projectFile">The project file to reload.</param>
    /// <param name="projectConfig">The config file of the project to reload.</param>
    /// <param name="verbose">Whether to show verbose output.</param>
    /// <param name="reloadData">Whether to reload the data file as well.</param>
    /// <returns><see cref="Command.CommandResult"/>.<see cref="Command.CommandResult.None"/> if no errors occured, if
    /// the project file can't get reloaded properly then <see cref="Command.CommandResult"/>.<see cref="Command.CommandResult.Quit"/>.</returns>
    private static Command.CommandResult ReloadCommand(IConsole console, ref ProjectFile projectFile, ProjectConfig projectConfig, bool verbose, bool reloadData)
    {
        return ReloadProject(console, ref projectFile, projectConfig, verbose, reloadData) ? Command.CommandResult.None : Command.CommandResult.Quit;
    }

    private static Command.CommandResult AddCommand(IConsole console, ProjectFile projectFile, string[] args)
    {
        if (args.Length != 3)
            return Command.CommandResult.InvalidSyntax;
        switch (args[1].ToLowerInvariant())
        {
            case "path":
            case "paths":
                AddAsset(console, args[2], projectFile.Paths, projectFile);
                break;
            case "sprite":
            case "sprites":
                AddAsset(console, args[2], projectFile.Sprites, projectFile);
                break;
            case "sound":
            case "sounds":
                AddAsset(console, args[2], projectFile.Sounds, projectFile);
                break;
            case "object":
            case "objects":
                AddAsset(console, args[2], projectFile.Objects, projectFile);
                break;
            case "background":
            case "backgrounds":
                AddAsset(console, args[2], projectFile.Backgrounds, projectFile);
                break;
            case "font":
            case "fonts":
                AddAsset(console, args[2], projectFile.Fonts, projectFile);
                break;
            case "room":
            case "rooms":
                AddAsset(console, args[2], projectFile.Rooms, projectFile);
                break;
            case "code":
                AddAsset(console, args[2], projectFile.Code, projectFile);
                break;
            default:
                return Command.CommandResult.InvalidSyntax;
        }

        return Command.CommandResult.None;
    }

    /// <summary>
    /// The apply command of the project shell. Compiles this project to a GameMaker data file.
    /// </summary>
    /// <param name="console">The console on where to output messages to.</param>
    /// <param name="projectFile">The project file which should get compiled.</param>
    /// <param name="projectConfig">The config file of the project which should get compiled.</param>
    /// <param name="verbose">Whether to output verbose messages.</param>
    /// <returns><see cref="Command.CommandResult"/>.<see cref="Command.CommandResult.None"/> if no errors occured, if
    /// the project file can't get reloaded properly after a compile then <see cref="Command.CommandResult"/>.<see cref="Command.CommandResult.Quit"/>.</returns>
    private static Command.CommandResult ApplyCommand(IConsole console, ref ProjectFile projectFile, ProjectConfig projectConfig, bool verbose)
    {
        try
        {
            projectFile.LoadAll();

            using FileStream fs = new FileStream(Path.Combine(projectConfig.OutputDirectory, projectFile.DataHandle.Filename), FileMode.Create);
            using GMDataWriter writer = new GMDataWriter(projectFile.DataHandle, fs, fs.Name, projectFile.DataHandle.Length);

            console.Output.WriteLine("Converting to data...");
            projectFile.ConvertToData();
            console.Output.WriteLine("Writing main data file...");
            writer.Write();
            writer.Flush();
            foreach (GMWarning warning in writer.Warnings)
                console.PrintGMWarning(warning);
        }
        catch (Exception e)
        {
            console.Error.WriteLine($"Failed to apply project: {e}");
        }

        if (!ReloadProject(console, ref projectFile, projectConfig, verbose))
            return Command.CommandResult.Quit;

        return Command.CommandResult.None;
    }

    /// <summary>
    /// The about command of the project shell. Shows some general info about a project.
    /// </summary>
    /// <param name="console">The console on where to output messages to.</param>
    /// <param name="projectConfig">The configuration file of the project.</param>
    /// <returns><see cref="Command.CommandResult"/>.<see cref="Command.CommandResult.None"/>.</returns>
    private static Command.CommandResult AboutCommand(IConsole console, ProjectConfig projectConfig)
    {
        console.Output.Write("Data file location: ");
        console.Output.WriteLine(projectConfig.InputFile);
        console.Output.Write("Output directory: ");
        console.Output.WriteLine(projectConfig.OutputDirectory);
        return Command.CommandResult.None;
    }

    #endregion

    private static void AddAsset<T>(IConsole console, string assets, AssetRefList<T> list, ProjectFile projectFile) where T : Asset
    {
        string[] assetSplit = assets.Split(',');
        List<int> indices = new List<int>();
        foreach (string asset in assetSplit)
        {
            int index = list.FindIndex(asset);
            if (index == -1)
            {
                console.Output.WriteLine($"Asset with name \"{asset}\" does not exist; ignoring.");
                continue;
            }

            if (list[index].Asset != null)
            {
                if (!console.PromptYesNo($"Asset \"{asset}\" is already in project. Overwrite?"))
                    continue;
            }
            indices.Add(index);
        }

        projectFile.AddAssetsToJSON(list, indices, true);
        projectFile.SaveAssets(list);
        projectFile.SaveMain();

        console.Output.WriteLine($"Added {indices.Count} assets.");
    }

    /// <summary>
    /// Reloads a DogScepter project.
    /// </summary>
    /// <param name="console">The console on where to output messages to.</param>
    /// <param name="projectFile">The project file to reload.</param>
    /// <param name="projectConfig">The config file of the project to reload.</param>
    /// <param name="verbose">Whether to show verbose output.</param>
    /// <param name="reloadData">Whether the data file should get reloaded as well.</param>
    /// <returns><see langword="true"/> if the project was successfully reloaded, <see langword="false"/> otherwise.</returns>
    private static bool ReloadProject(IConsole console, ref ProjectFile projectFile, ProjectConfig projectConfig, bool verbose, bool reloadData = true)
    {
        // Reload project/data file completely now
        console.Output.WriteLine("Reloading project...");
        GMData data;
        if (reloadData)
        {
            data = console.LoadDataFile(projectConfig.InputFile, verbose);
            if (data == null)
                return false;
        }
        else
            data = projectFile.DataHandle;
        projectFile = console.OpenProject(data, projectFile.DirectoryPath);
        if (projectFile == null)
            return false;

        try
        {
            projectFile.LoadAll();
        }
        catch (Exception e)
        {
            console.Error.WriteLine($"Failed to reload project: {e}");
            return false;
        }

        console.Output.WriteLine("Finished reload.");
        return true;
    }
}