using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DogScepter
{
    public class AboutWindow : Window
    {
        public AboutWindow()
        {
            DataContext = MainWindow.Instance;

            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
