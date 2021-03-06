using Avalonia.Data;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DogScepter
{
    public class TextExtension : MarkupExtension
    {
        public string Key { get; set; }

        public TextExtension(string key)
        {
            this.Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return new ReflectionBindingExtension($"[{Key}]")
            {
                Mode = BindingMode.OneWay,
                Source = MainWindow.Instance?.Text,
            }.ProvideValue(serviceProvider);
        }
    }
}
