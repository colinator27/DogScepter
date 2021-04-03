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

namespace DogScepter
{
    public class MainWindow : Window
    {
        public GMData DataFile;
        public ProjectFile ProjectFile;
        public Settings Settings;
        public Logger Logger;
        public TextData TextData;

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
            } catch (Exception e)
            {
                HandleException(e);
                Settings = new Settings();
            }
            try
            {
                TextData = new TextData();
                TextData.LoadLanguage(Settings.Language);
            } catch (Exception e)
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

        public void HandleException(Exception e)
        {
            if (Logger != null)
            {
                Logger.WriteLine("Exception: " + e.Message + "\n" + e?.StackTrace ?? "<null>");
                MessageBox.Avalonia.MessageBoxManager.GetMessageBoxStandardWindow("Error", e.Message).ShowDialog(this);
            }
        }

        public void UpdateTitle()
        {
            Title = "DogScepter";

            // Append version number to window title
            Title += " v" + Version;
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
