using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DogScepter
{
    public class Settings
    {
        public string Language { get; set; } = "en_US";

        public static JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public static void Save(Settings settings)
        {
            Storage.WriteAllBytes("settings.json", JsonSerializer.SerializeToUtf8Bytes(settings, JsonOptions));
        }

        public static Settings Load()
        {
            byte[] bytes = Storage.ReadAllBytes("settings.json");
            if (bytes == null)
                return new Settings();
            return JsonSerializer.Deserialize<Settings>(bytes, JsonOptions);
        }
    }
}
