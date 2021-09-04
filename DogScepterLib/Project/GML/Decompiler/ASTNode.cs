using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static DogScepterLib.Core.Models.GMCode.Bytecode;

namespace DogScepterLib.Project.GML.Decompiler
{
    /// <summary>
    /// This file contains the definitions of all AST nodes for GML output.
    /// It also contains the necessary code to write all of them to a string, recursively.
    /// </summary>

    public interface ASTNode
    {
        public enum StatementKind
        {
            Block,

            Int16,
            Int32,
            Int64,
            Float,
            Double,
            String,
            Boolean,
            Variable,

            TypeInst,

            Binary,
            Unary,

            Function,
            Assign,

            Break,
            Continue,
            Exit,
            Return,

            IfStatement,
            ShortCircuit,
            WhileLoop,
            ForLoop,
            DoUntilLoop,
            RepeatLoop,
            WithLoop,
            SwitchStatement,
            SwitchCase,
            SwitchDefault,
        }

        public StatementKind Kind { get; set; }
        public bool Duplicated { get; set; }
        public List<ASTNode> Children { get; set; }
        public void Write(DecompileContext ctx, StringBuilder sb);

        public static void Newline(DecompileContext ctx, StringBuilder sb)
        {
            sb.Append('\n');
            sb.Append(ctx.Indentation);
        }

        public static string WriteFromContext(DecompileContext ctx)
        {
            StringBuilder sb = new StringBuilder();

            ASTBlock block = ctx.BaseASTBlock;
            foreach (var child in block.Children)
            {
                child.Write(ctx, sb);
                Newline(ctx, sb);
            }

            return sb.ToString();
        }
    }

    public class ASTBlock : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Block;
        public bool Duplicated { get; set; }
        public List<ASTNode> Children { get; set; } = new List<ASTNode>();
        public ASTNode LoopParent { get; set; }
        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            sb.Append('{');
            ctx.IndentationLevel++;
            foreach (var child in Children)
            {
                ASTNode.Newline(ctx, sb);
                child.Write(ctx, sb);
            }
            ctx.IndentationLevel--;
            ASTNode.Newline(ctx, sb);
            sb.Append('}');
        }
    }

    public class ASTBreak : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Break;
        public bool Duplicated { get; set; }
        public List<ASTNode> Children { get; set; }
        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            sb.Append("break");
        }
    }

    public class ASTContinue : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Continue;
        public bool Duplicated { get; set; }
        public List<ASTNode> Children { get; set; }
        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            sb.Append("continue");
        }
    }

    public class ASTInt16 : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Int16;
        public bool Duplicated { get; set; }
        public List<ASTNode> Children { get; set; }

        public short Value;
        public Instruction.Opcode Opcode;
        public ASTInt16(short value, Instruction.Opcode opcode)
        {
            Value = value;
            Opcode = opcode;
        }

        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            sb.Append(Value);
        }
    }

    public class ASTInt32 : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Int32;
        public bool Duplicated { get; set; }
        public List<ASTNode> Children { get; set; }

        public int Value;
        public ASTInt32(int value) => Value = value;

        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            sb.Append(Value);
        }
    }

    public class ASTInt64 : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Int64;
        public bool Duplicated { get; set; }
        public List<ASTNode> Children { get; set; }

        public long Value;
        public ASTInt64(long value) => Value = value;

        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            sb.Append(Value);
        }
    }

    public class ASTFloat : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Float;
        public bool Duplicated { get; set; }
        public List<ASTNode> Children { get; set; }

        public float Value;
        public ASTFloat(float value) => Value = value;

        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            sb.Append(Value.ToString(null, CultureInfo.InvariantCulture));
        }
    }

    public class ASTDouble : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Double;
        public bool Duplicated { get; set; }
        public List<ASTNode> Children { get; set; }

        public double Value;
        public ASTDouble(double value) => Value = value;

        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            // TODO: implement this with better precision/predefined values
            sb.Append(Value.ToString(null, CultureInfo.InvariantCulture));
        }
    }

    public class ASTString : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.String;
        public bool Duplicated { get; set; }
        public List<ASTNode> Children { get; set; }

        public int Value;
        public ASTString(int value) => Value = value;

        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            string val = ctx.Strings[Value].Content;

            if (ctx.Data.VersionInfo.IsNumberAtLeast(2))
                val = "\"" + val.Replace("\\", "\\\\").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\"", "\\\"") + "\"";
            else
            {
                // Handle GM:S 1's lack of escaping
                bool front, back;
                if (val.StartsWith('"'))
                {
                    front = true;
                    val = val.Remove(0, 1);
                    if (val.Length == 0)
                        val = "'\"'";
                }
                else
                    front = false;
                if (val.EndsWith('"'))
                {
                    val = val.Remove(val.Length - 1);
                    back = true;
                }
                else
                    back = false;
                val = val.Replace("\"", "\" + '\"' + \"");
                if (front)
                    val = "'\"' + \"" + val;
                else
                    val = "\"" + val;
                if (back)
                    val += "\" + '\"'";
                else
                    val += "\"";
            }
            sb.Append(val);
        }
    }

    public class ASTBoolean : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Boolean;
        public bool Duplicated { get; set; }
        public List<ASTNode> Children { get; set; }

        public bool Value;
        public ASTBoolean(bool value) => Value = value;

        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            sb.Append(Value ? "true" : "false");
        }
    }

    public class ASTUnary : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Unary;
        public bool Duplicated { get; set; }
        public List<ASTNode> Children { get; set; }

        public Instruction Instruction;
        public ASTUnary(Instruction inst, ASTNode node)
        {
            Instruction = inst;
            Children = new() { node };
        }

        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            switch (Instruction.Kind)
            {
                case Instruction.Opcode.Neg:
                    sb.Append('-');
                    break;
                case Instruction.Opcode.Not:
                    if (Instruction.Type1 == Instruction.DataType.Boolean)
                        sb.Append('!');
                    else
                        sb.Append('~');
                    break;
            }
            Children[0].Write(ctx, sb);
        }
    }

    public class ASTBinary : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Binary;
        public bool Duplicated { get; set; }
        public List<ASTNode> Children { get; set; }

        public Instruction Instruction;
        public ASTBinary(Instruction inst, ASTNode left, ASTNode right)
        {
            Instruction = inst;
            Children = new() { left, right };
        }

        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            Children[0].Write(ctx, sb);

            string op = null;
            switch (Instruction.Kind)
            {
                case Instruction.Opcode.Mul: op = " * "; break;
                case Instruction.Opcode.Div: op = " / "; break;
                case Instruction.Opcode.Rem: op = " div "; break;
                case Instruction.Opcode.Mod: op = " % "; break;
                case Instruction.Opcode.Add: op = " + "; break;
                case Instruction.Opcode.Sub: op = " - "; break;
                case Instruction.Opcode.And:
                    if (Instruction.Type1 == Instruction.DataType.Boolean &&
                        Instruction.Type2 == Instruction.DataType.Boolean)
                        op = " && "; // Non-short-circuit
                    else
                        op = " & ";
                    break;
                case Instruction.Opcode.Or:
                    if (Instruction.Type1 == Instruction.DataType.Boolean &&
                        Instruction.Type2 == Instruction.DataType.Boolean)
                        op = " || "; // Non-short-circuit
                    else
                        op = " | ";
                    break;
                case Instruction.Opcode.Xor:
                    if (Instruction.Type1 == Instruction.DataType.Boolean &&
                        Instruction.Type2 == Instruction.DataType.Boolean)
                        op = " ^^ ";
                    else
                        op = " ^ ";
                    break;
                case Instruction.Opcode.Shl: op = " << "; break;
                case Instruction.Opcode.Shr: op = " >> "; break;
                case Instruction.Opcode.Cmp:
                    op = Instruction.ComparisonKind switch
                    {
                        Instruction.ComparisonType.LT => " < ",
                        Instruction.ComparisonType.LTE => " <= ",
                        Instruction.ComparisonType.EQ => " == ",
                        Instruction.ComparisonType.NEQ => " != ",
                        Instruction.ComparisonType.GTE => " >= ",
                        Instruction.ComparisonType.GT => " > ",
                        _ => null
                    };
                    break;
            }

            sb.Append(op);

            Children[1].Write(ctx, sb);
        }
    }

    public class ASTFunction : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Function;
        public bool Duplicated { get; set; }
        public List<ASTNode> Children { get; set; }

        public GMFunctionEntry Function;
        public ASTFunction(GMFunctionEntry function, List<ASTNode> args)
        {
            Function = function;
            Children = args;
        }

        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            bool arrayLiteral = Function.Name.Content == "@@NewGMLArray@@";

            if (arrayLiteral)
                sb.Append('[');
            else
            {
                sb.Append(Function.Name.Content);
                sb.Append('(');
            }

            if (Children.Count >= 1)
                Children[0].Write(ctx, sb);
            for (int i = 1; i < Children.Count; i++)
            {
                sb.Append(", ");
                Children[i].Write(ctx, sb);
            }

            if (arrayLiteral)
                sb.Append(']');
            else
                sb.Append(')');
        }
    }

    public class ASTExit : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Exit;
        public bool Duplicated { get; set; }
        public List<ASTNode> Children { get; set; }
        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            sb.Append("exit");
        }
    }

    public class ASTReturn : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Exit;
        public bool Duplicated { get; set; }
        public List<ASTNode> Children { get; set; }

        public ASTReturn(ASTNode arg) => Children = new() { arg };

        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            sb.Append("return ");
            Children[0].Write(ctx, sb);
        }
    }

    public class ASTVariable : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Variable;
        public bool Duplicated { get; set; }
        public List<ASTNode> Children { get; set; }

        public GMVariable Variable;
        public ASTNode Left;
        public Instruction.VariableType VarType;

        public ASTVariable(GMVariable var, Instruction.VariableType varType)
        {
            Variable = var;
            VarType = varType;
        }

        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            if (Left.Kind == ASTNode.StatementKind.Int16 ||
                Left.Kind == ASTNode.StatementKind.TypeInst)
            {
                int value;
                if (Left.Kind == ASTNode.StatementKind.Int16)
                    value = (Left as ASTInt16).Value;
                else
                    value = (Left as ASTTypeInst).Value;

                if (value < 0)
                {
                    // Builtin constants
                    switch (value)
                    {
                        case -5:
                            sb.Append("global.");
                            break;
                        case -2:
                            sb.Append("other.");
                            break;
                        case -3:
                            sb.Append("all.");
                            break;
                        case -16:
                            sb.Append("static.");
                            break;
                    }
                }
                else if (value < ctx.Project.Objects.Count)
                {
                    // Object names
                    sb.Append(ctx.Project.Objects[value].Name);
                    sb.Append('.');
                }
                else
                {
                    // Unknown number
                    sb.Append('(');
                    sb.Append(value);
                    sb.Append(").");
                }
            }
            else if (Left.Kind == ASTNode.StatementKind.Variable)
            {
                // Variable expression
                Left.Write(ctx, sb);
                sb.Append('.');
            }
            else
            {
                // Unknown expression
                sb.Append('(');
                Left.Write(ctx, sb);
                sb.Append(").");
            }

            // The actual variable name
            sb.Append(Variable.Name.Content);

            // Handle arrays
            if (ctx.Data.VersionInfo.IsNumberAtLeast(2, 3))
            {
                // ... for GMS2.3
                if (Children != null)
                {
                    foreach (var c in Children)
                    {
                        sb.Append('[');
                        c.Write(ctx, sb);
                        sb.Append(']');
                    }
                }
            }
            else if (Children != null)
            {
                // ... for pre-GMS2.3
                sb.Append('[');
                Children[0].Write(ctx, sb);
                if (Children.Count == 2)
                {
                    sb.Append(", ");
                    Children[1].Write(ctx, sb);
                }
                sb.Append(']');
            }
        }
    }

    public class ASTTypeInst : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.TypeInst;
        public bool Duplicated { get; set; }
        public List<ASTNode> Children { get; set; }

        public int Value;

        public ASTTypeInst(int value) => Value = value;

        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            // Doesn't really do anything on its own here
        }
    }

    public class ASTAssign : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Assign;
        public bool Duplicated { get; set; }
        public List<ASTNode> Children { get; set; }

        public ASTAssign(ASTVariable var, ASTNode node) => Children = new() { var, node };

        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            Children[0].Write(ctx, sb);
            sb.Append(" = "); // TODO others like +=
            Children[1].Write(ctx, sb);
        }
    }

    public class ASTIfStatement : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.IfStatement;
        public bool Duplicated { get; set; }
        public List<ASTNode> Children { get; set; } = new List<ASTNode>(3);

        public ASTIfStatement(ASTNode condition) => Children.Add(condition);

        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            sb.Append("if (");
            Children[0].Write(ctx, sb);
            sb.Append(')');

            // Main block
            if (Children[1].Kind != ASTNode.StatementKind.Block)
            {
                ctx.IndentationLevel++;
                ASTNode.Newline(ctx, sb);
                Children[1].Write(ctx, sb);
                ctx.IndentationLevel--;
            }
            else
            {
                ASTNode.Newline(ctx, sb);
                Children[1].Write(ctx, sb);
            }

            // Else block
            if (Children.Count >= 3)
            {
                ASTNode.Newline(ctx, sb);
                sb.Append("else");

                if (Children[2].Kind != ASTNode.StatementKind.Block)
                {
                    ctx.IndentationLevel++;
                    ASTNode.Newline(ctx, sb);
                    Children[2].Write(ctx, sb);
                    ctx.IndentationLevel--;
                }
                else
                {
                    ASTNode.Newline(ctx, sb);
                    Children[2].Write(ctx, sb);
                }
            }
        }
    }

    public class ASTShortCircuit : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.ShortCircuit;
        public bool Duplicated { get; set; }
        public List<ASTNode> Children { get; set; }

        public ShortCircuit.ShortCircuitType ShortCircuitKind;
        public ASTShortCircuit(ShortCircuit.ShortCircuitType kind, List<ASTNode> conditions)
        {
            ShortCircuitKind = kind;
            Children = conditions;
        }

        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            Children[0].Write(ctx, sb);
            string op = (ShortCircuitKind == ShortCircuit.ShortCircuitType.And) ? " && " : " || ";
            for (int i = 1; i < Children.Count; i++)
            {
                sb.Append(op);
                Children[i].Write(ctx, sb);
            }
        }
    }

    public class ASTWhileLoop : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.WhileLoop;
        public bool Duplicated { get; set; }
        public List<ASTNode> Children { get; set; } = new List<ASTNode>();
        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            sb.Append("while (");
            Children[1].Write(ctx, sb);
            sb.Append(')');

            // Main block
            if (Children[0].Kind != ASTNode.StatementKind.Block)
            {
                ctx.IndentationLevel++;
                ASTNode.Newline(ctx, sb);
                Children[0].Write(ctx, sb);
                ctx.IndentationLevel--;
            }
            else
            {
                ASTNode.Newline(ctx, sb);
                Children[0].Write(ctx, sb);
            }
        }
    }

    public class ASTForLoop : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.ForLoop;
        public bool Duplicated { get; set; }
        public List<ASTNode> Children { get; set; } = new List<ASTNode>();
        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            sb.Append("for (; ");
            Children[2].Write(ctx, sb);
            sb.Append("; ");
            Children[0].Write(ctx, sb);
            sb.Append(')');

            // Main block
            if (Children[1].Kind != ASTNode.StatementKind.Block)
            {
                ctx.IndentationLevel++;
                ASTNode.Newline(ctx, sb);
                Children[1].Write(ctx, sb);
                ctx.IndentationLevel--;
            }
            else
            {
                ASTNode.Newline(ctx, sb);
                Children[1].Write(ctx, sb);
            }
        }
    }

    public class ASTDoUntilLoop : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.DoUntilLoop;
        public bool Duplicated { get; set; }
        public List<ASTNode> Children { get; set; } = new List<ASTNode>();
        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            sb.Append("do");

            // Main block
            if (Children[0].Kind != ASTNode.StatementKind.Block)
            {
                ctx.IndentationLevel++;
                ASTNode.Newline(ctx, sb);
                Children[0].Write(ctx, sb);
                ctx.IndentationLevel--;
            }
            else
            {
                ASTNode.Newline(ctx, sb);
                Children[0].Write(ctx, sb);
            }

            ASTNode.Newline(ctx, sb);
            sb.Append("until (");
            Children[1].Write(ctx, sb);
            sb.Append(')');
        }
    }

    public class ASTRepeatLoop : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.RepeatLoop;
        public bool Duplicated { get; set; }
        public List<ASTNode> Children { get; set; } = new List<ASTNode>();

        public ASTRepeatLoop(ASTNode expr) => Children.Add(expr);
        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            sb.Append("repeat (");
            Children[1].Write(ctx, sb);
            sb.Append(')');

            // Main block
            if (Children[0].Kind != ASTNode.StatementKind.Block)
            {
                ctx.IndentationLevel++;
                ASTNode.Newline(ctx, sb);
                Children[0].Write(ctx, sb);
                ctx.IndentationLevel--;
            }
            else
            {
                ASTNode.Newline(ctx, sb);
                Children[0].Write(ctx, sb);
            }
        }
    }

    public class ASTWithLoop : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.WithLoop;
        public bool Duplicated { get; set; }
        public List<ASTNode> Children { get; set; } = new List<ASTNode>();

        public ASTWithLoop(ASTNode expr) => Children.Add(expr);
        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            sb.Append("with (");
            Children[1].Write(ctx, sb);
            sb.Append(')');

            // Main block
            if (Children[0].Kind != ASTNode.StatementKind.Block)
            {
                ctx.IndentationLevel++;
                ASTNode.Newline(ctx, sb);
                Children[0].Write(ctx, sb);
                ctx.IndentationLevel--;
            }
            else
            {
                ASTNode.Newline(ctx, sb);
                Children[0].Write(ctx, sb);
            }
        }
    }

    public class ASTSwitchStatement : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.SwitchStatement;
        public bool Duplicated { get; set; }
        public List<ASTNode> Children { get; set; } = new List<ASTNode>();
        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            sb.Append("switch (");
            Children[0].Write(ctx, sb);
            sb.Append(')');

            ASTNode.Newline(ctx, sb);
            sb.Append('{');
            ctx.IndentationLevel += 2;
            for (int i = 1; i < Children.Count; i++)
            {
                var child = Children[i];
                if (child.Kind == ASTNode.StatementKind.SwitchCase ||
                    child.Kind == ASTNode.StatementKind.SwitchDefault)
                {
                    ctx.IndentationLevel--;
                    ASTNode.Newline(ctx, sb);
                    child.Write(ctx, sb);
                    ctx.IndentationLevel++;
                }
                else
                {
                    ASTNode.Newline(ctx, sb);
                    child.Write(ctx, sb);
                }
            }
            ctx.IndentationLevel -= 2;
            ASTNode.Newline(ctx, sb);
            sb.Append('}');
        }
    }

    public class ASTSwitchCase : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.SwitchCase;
        public bool Duplicated { get; set; }
        public List<ASTNode> Children { get; set; }

        public ASTSwitchCase(ASTNode expr) => Children = new() { expr };

        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            sb.Append("case ");
            Children[0].Write(ctx, sb);
            sb.Append(':');
        }
    }

    public class ASTSwitchDefault : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.SwitchDefault;
        public bool Duplicated { get; set; }
        public List<ASTNode> Children { get; set; }
        public void Write(DecompileContext ctx, StringBuilder sb)
        {
            sb.Append("default:");
        }
    }
}
