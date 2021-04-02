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
            MainWindow.OpenFolder(Storage.DataDirectory + "\\test");
        }
    }
}
