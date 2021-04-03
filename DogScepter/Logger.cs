using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DogScepter
{
    public class Logger : IDisposable
    {
        private StreamWriter writer;

        public Logger()
        {
            if (Storage.FileSize("log.txt") >= 1024 * 1024 * 8)
                Storage.Rename("log.txt", "log.old.txt");

            writer = Storage.AppendText("log.txt");
            writer.WriteLine($"===============================\n{DateTime.Now.ToLongTimeString()} {DateTime.Now.ToLongDateString()}\nDogScepter {MainWindow.Version}\n===============================");
            writer.Flush();
        }

        public void Write(string text)
        {
            if (writer != null)
            {
                writer.Write(text);
                writer.Flush();
            }
            Console.Write(text);
        }

        public void WriteLine(string text)
        {
            if (writer != null)
            {
                writer.WriteLine(text);
                writer.Flush();
            }
            Console.WriteLine(text);
        }

        public void Dispose()
        {
            if (writer != null)
            {
                writer.Flush();
                writer.Close();
                writer.Dispose();
            }
        }
    }
}
