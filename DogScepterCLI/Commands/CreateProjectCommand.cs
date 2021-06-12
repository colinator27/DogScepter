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
    [Command("create", Description = "Creates a new DogScepter project.")]
    public class CreateProjectCommand : ICommand
    {
        [CommandOption("input", 'i', Description = "Input data file path.")]
        public string DataFile { get; private set; } = null;

        [CommandOption("output", 'o', Description = "Output directory.")]
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

            if (Interactive)
            {
                // Perform basic prompts to initialize the project
                DataFile ??= console.PromptFile("Enter location of data file");
                OutputDirectory ??= console.PromptDirectory("Enter directory to output files to");

                console.Output.WriteLine();
                console.Output.WriteLine("Project details");
                console.Output.WriteLine("===============");
                console.Output.WriteLine($"Directory: {dir}");
                console.Output.WriteLine($"Data file: {DataFile}");
                console.Output.WriteLine($"Output directory: {OutputDirectory}");
                console.Output.WriteLine();
                if (!console.PromptYesNo("Are these details correct?"))
                {
                    console.Output.WriteLine("Bailing.");
                    return default;
                }
            }
            else
            {
                if (DataFile == null || OutputDirectory == null)
                {
                    console.Error.WriteLine("Missing arguments. Data file and output directory must be set.");
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

            if (!CheckExisting(console, dir))
                return default;

            // Initialize the project file
            GMData data = console.LoadDataFile(DataFile, Verbose);
            if (data == null)
                return default;
            ProjectFile pf = console.OpenProject(data, dir);
            if (pf == null)
                return default;
            if (!console.SaveProject(pf))
                return default;
            
            // Update machine config file
            MachineConfig cfg = MachineConfig.Load();
            var pcfg = new ProjectConfig(DataFile, OutputDirectory);
            cfg.EditProject(dir, pcfg);
            MachineConfig.Save(cfg);

            if (Interactive)
                ProjectShell.Run(console, pf, pcfg, Verbose);

            return default;
        }

        private bool CheckExisting(IConsole console, string dir)
        {
            if (!Directory.Exists(dir))
            {
                console.Error.WriteLine("Project directory does not exist.");
                return false;
            }

            if (File.Exists(Path.Combine(dir, "project.json")))
            {
                console.Error.WriteLine("A project already exists in this location.");
                return false;
            }

            return true;
        }
    }
}
