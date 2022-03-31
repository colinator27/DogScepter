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
    /// <summary>
    /// The "open" command, which opens an existing DogScepter project.
    /// </summary>
    [Command("open", Description = "Opens an existing DogScepter project.")]
    // ReSharper disable once UnusedType.Global
    public class OpenProjectCommand : ICommand
    {
        /// <summary>
        /// File path that should be associated with the DogScepter project, if there wasn't one associated already.
        /// </summary>
        [CommandOption("input", 'i', Description = "Input data file path, if necessary.")]
        public string DataFile { get; private set; } = null;

        /// <summary>
        /// Directory path on where to output compiled files. If <see langword="null"/>, then the current working directory should be used.
        /// </summary>
        [CommandOption("output", 'o', Description = "Output directory, if necessary.")]
        public string CompiledOutputDirectory { get; private set; } = null;

        /// <summary>
        /// Whether to show verbose output from operations.
        /// </summary>
        [CommandOption("verbose", 'v', Description = "Whether to show verbose output from operations.")]
        public bool Verbose { get; init; } = false;

        /// <summary>
        /// The path where the DogScepter project gets created. If <see langword="null"/>, then the current working directory should be used.
        /// </summary>
        [CommandOption("dir", 'd', Description = "If not the working directory, specifies the project location.")]
        public string ProjectDirectory { get; init; } = null;

        /// <summary>
        /// Whether to use an interactive shell.
        /// </summary>
        [CommandOption("int", Description = "Whether to use interactive shell.")]
        public bool Interactive { get; init; } = true;

        public ValueTask ExecuteAsync(IConsole console)
        {
            console.Output.WriteLine();

            string dir = ProjectDirectory ?? Environment.CurrentDirectory;
            if (!Util.CheckIfProjectExists(console, dir))
                return default;

            MachineConfig cfg = MachineConfig.Load();
            if (cfg.Projects.TryGetValue(dir, out ProjectConfig pcfg))
            {
                // We have a config for this project, but we need to verify it

                // Verify that the data file associated with the project still exists,
                // if not prompt for new data file / read it from arguments
                if (!File.Exists(pcfg.InputFile))
                {
                    console.Error.WriteLine("Data file linked to the project no longer exists!");
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

                // Verify that the compiled output directory associated with the project still exists,
                // if not prompt for new directory / read from arguments
                if (!Directory.Exists(pcfg.OutputDirectory))
                {
                    console.Error.WriteLine("Output directory no longer exists!");
                    if (Interactive)
                        CompiledOutputDirectory ??= console.PromptDirectory("Enter new directory to output files to");
                    else if (CompiledOutputDirectory == null)
                    {
                        console.Error.WriteLine("Missing arguments. Output directory must be set.");
                        return default;
                    }
                    else if (!Directory.Exists(CompiledOutputDirectory))
                    {
                        console.Error.WriteLine("Provided output directory also does not exist.");
                        return default;
                    }
                }
                else
                    CompiledOutputDirectory = pcfg.OutputDirectory;
            }
            else
            {
                // If this isn't in the machine config, we need to prompt for input/output
                if (Interactive)
                {
                    DataFile ??= console.PromptFile("Enter location of data file");
                    CompiledOutputDirectory ??= console.PromptDirectory("Enter directory to output files to");
                }
                else
                {
                    if (DataFile == null || CompiledOutputDirectory == null)
                    {
                        console.Error.WriteLine("Missing arguments. Data file and output directory must be set, as this project is not yet registered.");
                        return default;
                    }
                    if (!File.Exists(DataFile))
                    {
                        console.Error.WriteLine("Data file does not exist.");
                        return default;
                    }
                    if (!Directory.Exists(CompiledOutputDirectory))
                    {
                        console.Error.WriteLine("Output directory does not exist.");
                        return default;
                    }
                }
            }

            if (!Util.CheckIfProjectExists(console, dir))
                return default;

            // Save potential changes to config
            var newPcfg = new ProjectConfig(DataFile, CompiledOutputDirectory);
            cfg.EditProject(dir, newPcfg);
            MachineConfig.Save(cfg);

            // Initialize the project file
            GMData data = console.LoadDataFile(DataFile, Verbose);
            if (data == null)
                return default;
            ProjectFile pf = console.OpenProject(data, dir);
            if (pf == null)
                return default;

            if (Interactive)
                ProjectShell.Run(console, pf, newPcfg, Verbose);

            return default;
        }


    }
}
