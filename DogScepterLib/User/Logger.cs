using DogScepterLib.User;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DogScepterLib.User
{
    public class Logger : IDisposable
    {
        private StreamWriter writer;

        public Logger()
        {
            if (Storage.Data.FileSize("log.txt") >= 1024 * 1024 * 8)
                Storage.Data.Rename("log.txt", "log.old.txt");

            writer = Storage.Data.AppendText("log.txt");
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
