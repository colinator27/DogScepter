using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using DogScepterLib.Core;
using DogScepterLib.Project;
using DogScepterLib.User;
using System;
using System.IO;
using System.Threading.Tasks;

namespace DogScepterCLI.Commands;

/// <summary>
/// The "create" command, which creates a new DogScepter project.
/// </summary>
[Command("create", Description = "Creates a new DogScepter project.")]
// ReSharper disable once UnusedType.Global - used as a Command for CliFix
public class CreateProjectCommand : ICommand
{
    /// <summary>
    /// The path to a GameMaker data file, which will be associated with the DogScepter project.
    /// </summary>
    [CommandOption("input", 'i', Description = "Input data file path.")]
    // ReSharper disable once MemberCanBePrivate.Global RedundantDefaultMemberInitializer - used as an Option for CliFix
    public string DataFile { get; private set; } = null;

    /// <summary>
    /// The path where compiled files will be generated.
    /// </summary>
    [CommandOption("output", 'o', Description = "Output directory for compiled files.")]
    // ReSharper disable once MemberCanBePrivate.Global RedundantDefaultMemberInitializer - used as an Option for CliFix
    public string CompiledOutputDirectory { get; private set; } = null;

    /// <summary>
    /// Whether to show verbose output from operations.
    /// </summary>
    [CommandOption("verbose", 'v', Description = "Whether to show verbose output from operations.")]
    // ReSharper disable once MemberCanBePrivate.Global - used as an Option for CliFix
    public bool Verbose { get; init; } = false;

    /// <summary>
    /// The path where the DogScepter project gets created. If <see langword="null"/>, then the current working directory should be used.
    /// </summary>
    [CommandOption("dir", 'd', Description = "If not the working directory, specifies the project location.")]
    // ReSharper disable once MemberCanBePrivate.Global - used as an Option for CliFix
    public string ProjectDirectory { get; init; } = null;

    /// <summary>
    /// Whether to use an interactive mode.
    /// </summary>
    [CommandOption("int", Description = "Whether to use interactive shell.")]
    // ReSharper disable once MemberCanBePrivate.Global - used as an Option for CliFix
    public bool Interactive { get; init; } = true;

    public ValueTask ExecuteAsync(IConsole console)
    {
        console.Output.WriteLine();

        // The project directory where we want to check for. Will get asked on / set depending on if we're in Interactive mode or not.
        string dir;

        if (Interactive)
        {
            // Perform basic prompts to initialize the project
            DataFile ??= console.PromptFile("Enter location of data file");
            dir = ProjectDirectory ?? console.PromptDirectory("Enter location of the project directory (\".\" for current directory)");
            CompiledOutputDirectory ??= console.PromptDirectory("Enter directory to output compiled files to");

            console.Output.WriteLine();
            console.Output.WriteLine("Project details");
            console.Output.WriteLine("===============");
            console.Output.WriteLine($"Data file: {DataFile}");
            console.Output.WriteLine($"Project directory: {dir}");
            console.Output.WriteLine($"Output directory for compiled files: {CompiledOutputDirectory}");
            console.Output.WriteLine();
            if (!console.PromptYesNo("Are these details correct?"))
            {
                console.Output.WriteLine("Bailing.");
                return default;
            }
        }
        else
        {
            dir = ProjectDirectory ?? Environment.CurrentDirectory;

            if (DataFile == null || CompiledOutputDirectory == null)
            {
                console.Error.WriteLine("Missing arguments. Data file and output directory for compiled files must be set.");
                return default;
            }
            if (!File.Exists(DataFile))
            {
                console.Error.WriteLine("Data file does not exist.");
                return default;
            }
            //TODO: maybe have feature to automatically create folders that don't exist?
            if (!Directory.Exists(CompiledOutputDirectory))
            {
                console.Error.WriteLine("Output directory for compiled files does not exist.");
                return default;
            }
        }

        if (Util.CheckIfProjectExists(console, dir, true))
            return default;

        // Initialize the project file
        console.Output.WriteLine("Creating project...");
        GMData data = console.LoadDataFile(DataFile, Verbose);
        if (data == null)
            return default;
        ProjectFile projectFile = console.OpenProject(data, dir);
        if (projectFile == null)
            return default;
        if (!console.SaveProject(projectFile))
            return default;

        // Update machine config file
        MachineConfig machineCfg = MachineConfig.Load();
        ProjectConfig projectCfg = new ProjectConfig(DataFile, CompiledOutputDirectory);
        machineCfg.EditProject(dir, projectCfg);
        MachineConfig.Save(machineCfg);

        if (Interactive)
            ProjectShell.Run(console, projectFile, projectCfg, Verbose);

        return default;
    }
}