using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace DogScepter
{
    public class LoaderDialog : Window
    {

        public LoaderDialog()
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            CanResize = false;

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
