using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DogScepterLib.Project.GML.Compiler;

public enum NodeKind
{
    Empty,
    Group, // Group of nodes, used inside other nodes
    Block, // Group of statements
    Constant, // Represents basic number/string data type in the code

    FunctionCall, // Represents a place where a function is called (but can also be a variable name)
    FunctionCallChain, // Appears in cases like a.b() and a()(). First child is a ChainReference or another FunctionCallChain
    FunctionCallExpr, // Appears specifically in cases like a()(), representing the second call in this case
    Variable, // Represents a single variable name (and nothing else)
    VariableAccessor, // Variable name followed by any number of accessors
    Accessor, // Individual accessor (has sub-expressions)
    ChainReference, // Appears in all cases of a.b, etc.
    Prefix, // Appears in cases like ++a, ++a.b.c, ++a.b[0] (and so on)
    Postfix, // Appears in cases like a++, a.b.c++, a.b[0]++ (and so on)

    Conditional, // like (a ? b : c)
    NullCoalesce, // like (a ?? b)
    Binary, // like (a == b) and most other operators, including arithmetic
    Unary, // like !a (and other operations)

    Assign, // like a = 123
    Exit,
    Break,
    Continue,
    Return,
    If,
    Switch,
    SwitchCase,
    SwitchDefault,
    LocalVarDecl, // like var a = 123, etc.
    With,
    While,
    For,
    Repeat,
    DoUntil,

    FunctionDecl, // like function(){}, but also used internally for structs
    Static, // used for static groupings
    New,
}

public class Node
{
    public NodeKind Kind { get; set; }
    public Token Token { get; set; }
    public List<Node> Children { get; set; } = new();
    public INodeInfo Info { get; set; }

    private static readonly Token NullToken = new(null, -1);

    public Node(NodeKind kind)
    {
        Kind = kind;
        Token = NullToken;
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

    public override string ToString()
    {
        if (Info != null)
            return $"Node: {Kind} ({Info}) > {Children.Count} children";
        if (Kind == NodeKind.Variable)
            return $"Node: {Kind} [{Token.Value}] > {Children.Count} children";
        if (Token != NullToken)
            return $"Node: {Kind} ({Token}) > {Children.Count} children";
        return $"Node: {Kind} > {Children.Count} children";
    }
}

public interface INodeInfo
{
}

public class NodeFunctionInfo : INodeInfo
{
    public FunctionReference Reference { get; set; }
    public bool IsConstructor { get; set; }
    public List<string> LocalVars { get; set; }
    public List<string> StaticVars { get; set; }
    public List<string> Arguments { get; set; }
    public int InheritingIndex { get; set; }
    public int OptionalArgsIndex { get; set; }

    public NodeFunctionInfo(FunctionReference reference, bool isConstructor, List<string> localVars, List<string> staticVars, List<string> arguments)
    {
        Reference = reference;
        IsConstructor = isConstructor;
        LocalVars = localVars;
        StaticVars = staticVars;
        Arguments = arguments;
    }

    public override string ToString()
    {
        return $"Func Info: constructor={IsConstructor}, locals={LocalVars.Count}, statics={StaticVars.Count}, " +
                $"args={Arguments.Count}, inherits={InheritingIndex != -1}, optionals={OptionalArgsIndex != -1}";
    }
}

public class NodeAccessorInfo : INodeInfo
{
    public readonly static Dictionary<TokenKind, NodeAccessorInfo> Accessors = new()
    {
        { TokenKind.ArrayOpen, new(TokenKind.ArrayOpen, true) },
        { TokenKind.ArrayListOpen, new(TokenKind.ArrayListOpen, true) },
        { TokenKind.ArrayMapOpen, new(TokenKind.ArrayMapOpen, false) },
        { TokenKind.ArrayGridOpen, new(TokenKind.ArrayGridOpen, true) },
        { TokenKind.ArrayDirectOpen, new(TokenKind.ArrayDirectOpen, true) },
        { TokenKind.ArrayStructOpen, new(TokenKind.ArrayStructOpen, false) }
    };

    public TokenKind Kind { get; set; }
    public bool DisallowStrings { get; set; }

    public NodeAccessorInfo(TokenKind kind, bool verifyInteger)
    {
        Kind = kind;
        DisallowStrings = verifyInteger;
    }
    public override string ToString()
    {
        return $"Accessor ({Kind})";
    }
}