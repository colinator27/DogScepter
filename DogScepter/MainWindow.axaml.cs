using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.Diagnostics;
using System.Reflection;
using DogScepterLib.Core;
using DogScepterLib.Project;
using System.Runtime.InteropServices;

namespace DogScepter
{
    public class MainWindow : Window
    {
        public GMData? DataFile = null;
        public ProjectFile? ProjectFile = null;
        public Text? Text = null;

        public static MainWindow? Instance = null;
        public static string Version = (FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion ?? "????");

        public MainWindow()
        {
            Instance = this;
            DataContext = this;
            Text = new Text();
            Text.LoadLanguage("en_US"); // todo: get settings

            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif

            UpdateTitle();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public void UpdateTitle()
        {
            Title = "DogScepter";

            // Append version number to window title
            Title += " v" + Version;
        }

        public void Menu_Exit()
        {
            // TODO: cleanup, also prompt for saving changes potentially
            Close();
        }

        public async void Menu_About()
        {
            var window = new AboutWindow();
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
        }
    }
}
