using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using DogScepterLib.User;

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
            Storage.Data.CreateDirectory();
            MainWindow.OpenFolder(Storage.Data.Location);
        }

        // TODO: add a button for Config directory

        public void Button_OpenTempDirectory()
        {
            Storage.Temp.CreateDirectory();
            MainWindow.OpenFolder(Storage.Temp.Location);
        }

        public void Button_ClearTempDirectory()
        {
            Storage.Temp.Clear();
            var text = MainWindow.Instance?.TextData;
            if (text == null)
                return;
            MainWindow.Instance?.ShowMessage(text["info.title"], text["info.cleared_temp"], this);
        }
    }
}
