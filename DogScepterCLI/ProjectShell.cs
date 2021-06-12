using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using DogScepterLib.Project;
using DogScepterLib.Project.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DogScepterCLI
{
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

        public static void Run(IConsole console, ProjectFile pf)
        {
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

                new Command(new[] { "export" },
                "Exports an asset from game data to disk.",
                "export <asset_type> <asset_names>",
                args =>
                {
                    if (args.Length != 3)
                        return Command.CommandResult.InvalidSyntax;
                    switch (args[1].ToLowerInvariant())
                    {
                        case "path":
                        case "paths":
                            {
                                // todo, split by commas, and also abstract this code out
                                int ind = pf.Paths.FindIndex(args[2]);
                                if (ind == -1)
                                {
                                    console.Output.WriteLine($"Asset with name \"{args[2]}\" does not exist.");
                                }
                                else
                                {
                                    if (pf.Paths[ind].Asset != null)
                                    {
                                        if (!console.PromptYesNo($"Asset \"{args[2]}\" is already in project. Overwrite?"))
                                            break;
                                    }

                                    pf.GetConverter<PathConverter>().ConvertData(pf, ind);
                                    pf.Paths[ind].Asset.Dirty = true;
                                    pf.AddDirtyAssetsToJSON(pf.Paths, "paths");
                                    pf.SaveAll();
                                }
                            }
                            break;
                    }
                    return Command.CommandResult.None;
                }),
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
}
