using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DogScepterLib.Project.GML.Compiler
{
    public class CompileContext
    {
        public ProjectFile Project { get; init; }
        public string Code { get; init; }
        public int Position { get; set; } = 0;
        public List<Token> Tokens { get; set; } = null;
        public bool IsGMS2 { get; init; }
        public bool IsGMS23 { get; init; }
        public List<ErrorMessage> Errors { get; init; } = new();

        public CompileContext(ProjectFile pf, string code)
        {
            Project = pf;
            IsGMS2 = pf.DataHandle.VersionInfo.IsNumberAtLeast(2);
            IsGMS23 = pf.DataHandle.VersionInfo.IsNumberAtLeast(2, 3);
            Code = code;
        }

        public void Error(string message, Token token)
        {
            // Count lines/columns
            int line = 1;
            int column = 1;
            for (int i = 0; i < token.Index; i++)
            {
                if (Code[i] == '\n')
                {
                    line++;
                    column = 1;
                }
                else
                    column++;
            }

            Errors.Add(new(message, line, column));
        }
    }

    public class ErrorMessage
    {
        public string Message { get; init; }
        public int Line { get; init; }
        public int Column { get; init; }

        public ErrorMessage(string message, int line, int column)
        {
            Message = message;
            Line = line;
            Column = column;
        }
    }
}
