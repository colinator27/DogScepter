using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DogScepterLib.Project.GML.Compiler
{
    public enum NodeKind
    {
        Block,
        Constant, // Represents basic number/string data type in the code

        FunctionCall, // Represents a place where a function is called (but can also be a variable name)
        FunctionCallChain, // Appears in cases like a.b() and a()(). First child is a ChainReference or another FunctionCallChain
        Variable, // Represents a single variable name (and nothing else)
        ChainReference, // Appears in cases like a.b.c and a.b[0].c (and so on). 
        Prefix, // Appears in cases like ++a, ++a.b.c, ++a.b[0] (and so on)
        Postfix, // Appears in cases like a++, a.b.c++, a.b[0]++ (and so on)
        Accessor, // Arrays and accessors

        Conditional, // like (a ? b : c)
        NullCoalesce, // like (a ?? b)
        Binary, // like (a == b) and most other operators, including arithmetic
        Unary, // like !a (and other operations)
    }

    public class Node
    {
        public NodeKind Kind { get; set; }
        public Token Token { get; set; }
        public List<Node> Children { get; set; } = new();

        public Node(NodeKind kind)
        {
            Kind = kind;
            Token = new Token(null, -1);
        }

        public Node(NodeKind kind, Token token)
        {
            Kind = kind;
            Token = token;
        }

        public Node(CodeContext context, BuiltinVariable builtin)
        {
            Kind = NodeKind.Variable;
            Token = new Token(context, new TokenVariable(builtin.Name, builtin), -1);
        }
    }
}
