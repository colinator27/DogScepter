using Avalonia;
using Avalonia.Platform;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;

namespace DogScepter
{
    // Code based on https://www.sakya.it/wordpress/avalonia-ui-framework-localization/
    public class Text : INotifyPropertyChanged
    {
        public string Language;
        public string this[string key]
        {
            get
            {
                string res;
                if (strings != null && strings.TryGetValue(key, out res))
                {
                    res = res.Replace("~ver", MainWindow.Version);
                    return res;
                }

                return $"{key}";
            }
        }

        private Dictionary<string, string>? strings = null;

        public event PropertyChangedEventHandler? PropertyChanged;

        public Text()
        {

        }

        /// <summary>
        /// Loads a JSON language file with the given name (without extension). Throws an exception if unsuccessful.
        /// </summary>
        public void LoadLanguage(string name)
        {
            Language = name;

            var assets = AvaloniaLocator.Current.GetService<IAssetLoader>();

            Uri uri = new Uri($"avares://DogScepter/Assets/Language/{name}.json");
            if (assets.Exists(uri))
            {
                using (StreamReader sr = new StreamReader(assets.Open(uri), Encoding.UTF8))
                {
                    strings = JsonSerializer.Deserialize<Dictionary<string, string>>(sr.ReadToEnd());
                    if (strings == null)
                        throw new Exception($"Malformed language JSON {name}");
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item"));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
                }
            }
            else
                throw new Exception($"Nonexistent language JSON {name}");
        }
    }
}
