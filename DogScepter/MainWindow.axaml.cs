using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.Diagnostics;
using System.Reflection;
using DogScepterLib.Core;
using DogScepterLib.Project;
using DogScepter.Localization;
using System.Runtime.InteropServices;
using System;
using System.ComponentModel;
using System.IO;
using System.Collections.Generic;
using MessageBox.Avalonia.DTO;
using MessageBox.Avalonia.Enums;
using System.Threading.Tasks;
using System.Threading;
using DogScepterLib.User;

namespace DogScepter
{
    public class MainWindow : Window
    {
        public GMData DataFile;
        public ProjectFile ProjectFile;
        public Settings Settings;
        public Logger Logger;
        public TextData TextData;

        public ProjectTree Tree = new ProjectTree();

        public LoaderDialog? Loader;

        public static MainWindow? Instance = null;
        public static string Version = (FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion ?? "????");

        public bool CleanedUp { get; private set; } = false;

        public MainWindow()
        {
            Instance = this;
            DataContext = this;
            try
            {
                Logger = new Logger();
                Settings = Settings.Load();
            } 
            catch (Exception e)
            {
                HandleException(e);
                Settings = new Settings();
            }

            try
            {
                TextData = new TextData();
                TextData.LoadLanguage(Settings.Language);
            } 
            catch (Exception e)
            {
                HandleException(e);
            }

            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif

            Closing += (object? sender, CancelEventArgs e) => Cleanup(); // TODO prompt for saving

            UpdateTitle();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public void HandleException(Exception e, Window? owner = null)
        {
            if (Logger != null)
            {
                Logger.WriteLine("Exception: " + e.Message + "\n" + e?.StackTrace ?? "<null>");
                ShowMessage(TextData["error.title"], e.Message, owner);
            }
        }

        public void ShowMessage(string title, string message, Window? owner = null)
        {
            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                // TODO replace this with a custom message box, this one doesn't work well
                MessageBoxStandardParams p = new MessageBoxStandardParams()
                {
                    ContentTitle = title,
                    ContentMessage = message,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    ButtonDefinitions = ButtonEnum.Ok,
                    Style = Style.Windows
                };
                MessageBox.Avalonia.MessageBoxManager.GetMessageBoxStandardWindow(p).ShowDialog(owner ?? this);
            });
        }

        public void UpdateTitle()
        {
            Title = "DogScepter";

            // Append version number to window title
            Title += " v" + Version;
        }

        public void UpdateStatus(string status)
        {
            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (Loader?.IsActive == true)
                {
                    Loader.Find<TextBlock>("Status").Text = status;
                }
            });
        }

        public void Cleanup()
        {
            if (CleanedUp)
                return;

            CleanedUp = true;

            if (Logger != null)
            {
                Logger.WriteLine("Exiting...");
                Logger.Dispose();
            }
        }

        public async void Menu_Settings()
        {
            SettingsWindow window = new SettingsWindow()
            {
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            await window.ShowDialog(this);
        }

        public void Menu_Exit()
        {
            // TODO: prompt for saving changes potentially
            Cleanup();
            Close();
        }

        public async Task<bool> Menu_OpenData()
        {
            if (ProjectFile != null)
            {
                // TODO dialog asking for saving
            }

            OpenFileDialog dialog = new OpenFileDialog()
            {
                AllowMultiple = false,
                InitialFileName = "data.win",
                Filters = new List<FileDialogFilter>()
                {
                    new FileDialogFilter() { Name = "GameMaker data files", Extensions = { "win", "ios", "unx", "droid" } },
                    new FileDialogFilter() { Name = "All files", Extensions = { "*" } }
                }
            };

            string[] result = await dialog.ShowAsync(this);
            if (result != null && result.Length == 1)
            {
                string file = result[0];
                if (File.Exists(file))
                {
                    Loader = new LoaderDialog();
                    _ = Loader.ShowDialog(this);
                    Task<bool> t = Task.Run(() =>
                    {
                        try
                        {
                            using (FileStream fs = new FileStream(file, FileMode.Open))
                            {
                                UpdateStatus("Loading data file...");
                                Logger.WriteLine($"Loading data file {file}");

                                GMDataReader reader = new GMDataReader(fs, fs.Name);
                                foreach (GMWarning w in reader.Warnings)
                                    Logger.WriteLine(string.Format("[WARN: {0}] {1}", w.Level, w.Message));
                                DataFile = reader.Data;
                                DataFile.Logger = Logger.WriteLine;

                                Logger.WriteLine("Finished loading data file");

                                UpdateStatus("Creating temporary project...");
                                Logger.WriteLine("Making temp project");

                                string? err = Storage.Temp.Clear();
                                if (err != null)
                                    throw new IOException(err);
                                ProjectFile = new ProjectFile(DataFile, Storage.Temp.Location,
                                    (ProjectFile.WarningType type, string info) =>
                                    {
                                        Logger.WriteLine($"Project warn: {type} {info ?? ""}");
                                    });

                                Logger.WriteLine("Finished making temp project");

                                return true;
                            }
                        }
                        catch (Exception e)
                        {
                            HandleException(e);
                        }
                        return false;
                    });
                    await t;
                    Loader.Close();
                } else
                {
                    ShowMessage(TextData["error.title"], TextData["error.file_exists"]);
                    return false;
                }
            }

            return true;
        }

        public async void Menu_About()
        {
            AboutWindow window = new AboutWindow()
            {
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            await window.ShowDialog(this);
        }

        public void Menu_GitHub()
        {
            OpenBrowser("https://github.com/colinator27/dog-scepter");
        }

        public void Menu_ReportIssues()
        {
            OpenBrowser("https://github.com/colinator27/dog-scepter/issues");
        }

        /// From https://github.com/AvaloniaUI/Avalonia/blob/master/src/Avalonia.Dialogs/AboutAvaloniaDialog.xaml.cs
        public static void OpenBrowser(string url)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    using (var process = Process.Start(
                        new ProcessStartInfo
                        {
                            FileName = "/bin/sh",
                            Arguments = $"-c \"{$"xdg-open {url}".Replace("\"", "\\\"")}\"",
                            RedirectStandardOutput = true,
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            WindowStyle = ProcessWindowStyle.Hidden
                        }
                    )) { }
                }
                else
                {
                    using (var process = Process.Start(new ProcessStartInfo
                    {
                        FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? url : "open",
                        Arguments = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? $"{url}" : "",
                        CreateNoWindow = true,
                        UseShellExecute = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    })) { }
                }
            } catch (Exception e)
            {
                Instance?.HandleException(e);
            }
        }

        public static void OpenFolder(string folder)
        {
            if (!folder.EndsWith(Path.DirectorySeparatorChar))
                folder += Path.DirectorySeparatorChar;

            try
            {
                Process.Start(new ProcessStartInfo()
                {
                    FileName = folder,
                    UseShellExecute = true,
                    Verb = "Open"
                });
            } catch (Exception e)
            {
                Instance?.HandleException(e);
            }
        }
    }
}
