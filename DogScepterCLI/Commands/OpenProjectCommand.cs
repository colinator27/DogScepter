using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using DogScepterLib.Core;
using DogScepterLib.Project;
using DogScepterLib.User;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DogScepterCLI.Commands
{
    [Command("open", Description = "Opens an existing DogScepter project.")]
    public class OpenProjectCommand : ICommand
    {
        [CommandOption("input", 'i', Description = "Input data file path, if necessary.")]
        public string DataFile { get; private set; } = null;

        [CommandOption("output", 'o', Description = "Output directory, if necessary.")]
        public string OutputDirectory { get; private set; } = null;

        [CommandOption("verbose", 'v', Description = "Whether to show verbose output from operations.")]
        public bool Verbose { get; init; } = false;

        [CommandOption("dir", 'd', Description = "If not the working directory, specifies the project location.")]
        public string ProjectDirectory { get; init; } = null;

        [CommandOption("int", Description = "Whether to use interactive shell.")]
        public bool Interactive { get; init; } = true;

        public ValueTask ExecuteAsync(IConsole console)
        {
            console.Output.WriteLine();

            string dir = ProjectDirectory ?? Environment.CurrentDirectory;
            if (!CheckExisting(console, dir))
                return default;

            MachineConfig cfg = MachineConfig.Load();
            if (cfg.Projects.TryGetValue(dir, out ProjectConfig pcfg))
            {
                // We have a config for this project, but we need to verify it
                if (!File.Exists(pcfg.InputFile))
                {
                    console.Error.WriteLine("Data file no longer exists!");
                    if (Interactive)
                        DataFile ??= console.PromptFile("Enter new location of data file");
                    else if (DataFile == null)
                    {
                        console.Error.WriteLine("Missing arguments. Data file must be set.");
                        return default;
                    }
                    else if (!File.Exists(DataFile))
                    {
                        console.Error.WriteLine("Provided data file also does not exist.");
                        return default;
                    }
                }
                else
                    DataFile = pcfg.InputFile;

                if (!Directory.Exists(pcfg.OutputDirectory))
                {
                    console.Error.WriteLine("Output directory no longer exists!");
                    if (Interactive)
                        OutputDirectory ??= console.PromptDirectory("Enter new directory to output files to");
                    else if (OutputDirectory == null)
                    {
                        console.Error.WriteLine("Missing arguments. Output directory must be set.");
                        return default;
                    }
                    else if (!Directory.Exists(OutputDirectory))
                    {
                        console.Error.WriteLine("Provided output directory also does not exist.");
                        return default;
                    }
                }
                else
                    OutputDirectory = pcfg.OutputDirectory;
            }
            else
            {
                // If this isn't in the machine config, we need to prompt for input/output
                if (Interactive)
                {
                    DataFile ??= console.PromptFile("Enter location of data file");
                    OutputDirectory ??= console.PromptDirectory("Enter directory to output files to");
                }
                else
                {
                    if (DataFile == null || OutputDirectory == null)
                    {
                        console.Error.WriteLine("Missing arguments. Data file and output directory must be set, as this project is not yet registered.");
                        return default;
                    }
                    if (!File.Exists(DataFile))
                    {
                        console.Error.WriteLine("Data file does not exist.");
                        return default;
                    }
                    if (!Directory.Exists(OutputDirectory))
                    {
                        console.Error.WriteLine("Output directory does not exist.");
                        return default;
                    }
                }
            }

            if (!CheckExisting(console, dir))
                return default;

            // Save potential changes to config
            cfg.EditProject(dir, new ProjectConfig(DataFile, OutputDirectory));
            MachineConfig.Save(cfg);

            // Initialize the project file
            GMData data = console.LoadDataFile(DataFile, Verbose);
            if (data == null)
                return default;
            ProjectFile pf = console.OpenProject(data, dir);

            if (Interactive)
                ProjectShell.Run(console, pf);

            return default;
        }

        private bool CheckExisting(IConsole console, string dir)
        {
            if (!Directory.Exists(dir))
            {
                console.Error.WriteLine("Project directory does not exist.");
                return false;
            }

            if (!File.Exists(Path.Combine(dir, "project.json")))
            {
                console.Error.WriteLine("No project exists in this location.");
                return false;
            }

            return true;
        }
    }
}
