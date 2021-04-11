using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DogScepter
{
    public class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            DataContext = this;

            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public void Button_OpenDataDirectory()
        {
            MainWindow.OpenFolder(Storage.DataDirectory);
        }

        public void Button_OpenTempDirectory()
        {
            Storage.GetTempDirectory();
            MainWindow.OpenFolder(Storage.TempDirectory);
        }

        public void Button_ClearTempDirectory()
        {
            Storage.ClearTempDirectory();
            var text = MainWindow.Instance?.TextData;
            if (text == null)
                return;
            MainWindow.Instance?.ShowMessage(text["info.title"], text["info.cleared_temp"], this);
        }
    }
}
