using DogScepterLib.Core;
using DogScepterLib.Core.Chunks;
using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DogScepterLib.Project.GML.Decompiler
{
    public class DecompileContext
    {
        public ProjectFile Project { get; set; }
        public GMData Data { get; set; }
        public IList<GMString> Strings { get; set; }
        public BlockList Blocks { get; set; }
        public List<Loop> Loops { get; set; }
        public List<ShortCircuit> ShortCircuits { get; set; }
        public List<IfStatement> IfStatements { get; set; }
        public List<SwitchStatement> SwitchStatements { get; set; }
        public Node BaseNode { get; set; }
        public ASTBlock BaseASTBlock { get; set; }

        private int indentationLevel = 0;
        public int IndentationLevel
        {
            get => indentationLevel;
            set { indentationLevel = value; Indentation = new StringBuilder().Insert(0, Indent, indentationLevel).ToString(); }
        }
        public const string Indent = "    ";
        public string Indentation = "";

        public DecompileContext(ProjectFile pf)
        {
            Project = pf;
            Data = pf.DataHandle;
            Strings = Data.GetChunk<GMChunkSTRG>().List;
        }
    }

    public static class Decompiler
    {
        public static string Decompile(DecompileContext ctx)
        {
            // TODO
            return "// Stub";
        }
    }
}
