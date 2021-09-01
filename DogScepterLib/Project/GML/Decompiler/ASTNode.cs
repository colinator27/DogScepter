using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static DogScepterLib.Core.Models.GMCode.Bytecode;

namespace DogScepterLib.Project.GML.Decompiler
{
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

            If,
            ShortCircuit,
        }

        public StatementKind Kind { get; set; }
        public List<ASTNode> Children { get; set; }
    }

    public class ASTBlock : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Block;
        public List<ASTNode> Children { get; set; } = new List<ASTNode>();
    }

    public class ASTBreak : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Break;
        public List<ASTNode> Children { get; set; }
    }

    public class ASTContinue : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Continue;
        public List<ASTNode> Children { get; set; }
    }

    public class ASTInt16 : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Int16;
        public List<ASTNode> Children { get; set; }

        public short Value;
        public Instruction.Opcode Opcode;
        public ASTInt16(short value, Instruction.Opcode opcode)
        {
            Value = value;
            Opcode = opcode;
        }
    }

    public class ASTInt32 : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Int32;
        public List<ASTNode> Children { get; set; }

        public int Value;
        public ASTInt32(int value) => Value = value;
    }

    public class ASTInt64 : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Int64;
        public List<ASTNode> Children { get; set; }

        public long Value;
        public ASTInt64(long value) => Value = value;
    }

    public class ASTFloat : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Float;
        public List<ASTNode> Children { get; set; }

        public float Value;
        public ASTFloat(float value) => Value = value;
    }

    public class ASTDouble : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Float;
        public List<ASTNode> Children { get; set; }

        public double Value;
        public ASTDouble(double value) => Value = value;
    }

    public class ASTString : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.String;
        public List<ASTNode> Children { get; set; }

        public int Value;
        public ASTString(int value) => Value = value;
    }

    public class ASTBoolean : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Boolean;
        public List<ASTNode> Children { get; set; }

        public bool Value;
        public ASTBoolean(bool value) => Value = value;
    }

    public class ASTUnary : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Unary;
        public List<ASTNode> Children { get; set; }

        public Instruction Instruction;
        public ASTUnary(Instruction inst, ASTNode node)
        {
            Instruction = inst;
            Children = new() { node };
        }
    }

    public class ASTBinary : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Binary;
        public List<ASTNode> Children { get; set; }

        public Instruction Instruction;
        public ASTBinary(Instruction inst, ASTNode left, ASTNode right)
        {
            Instruction = inst;
            Children = new() { left, right };
        }
    }

    public class ASTFunction : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Function;
        public List<ASTNode> Children { get; set; }

        public GMFunctionEntry Function;
        public ASTFunction(GMFunctionEntry function, List<ASTNode> args)
        {
            Function = function;
            Children = args;
        }
    }

    public class ASTExit : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Exit;
        public List<ASTNode> Children { get; set; }
    }

    public class ASTReturn : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Exit;
        public List<ASTNode> Children { get; set; }

        public ASTReturn(ASTNode arg) => Children = new() { arg };
    }

    public class ASTVariable : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Variable;
        public List<ASTNode> Children { get; set; }

        public GMVariable Variable;
        public ASTNode Left;
        public Instruction.VariableType VarType;

        public ASTVariable(GMVariable var, Instruction.VariableType varType)
        {
            Variable = var;
            VarType = varType;
        }
    }

    public class ASTTypeInst : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.TypeInst;
        public List<ASTNode> Children { get; set; }

        public int Value;

        public ASTTypeInst(int value) => Value = value;
    }

    public class ASTAssign : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.Assign;
        public List<ASTNode> Children { get; set; }

        public ASTAssign(ASTVariable var, ASTNode node) => Children = new() { var, node };
    }

    public class ASTIfStatement : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.If;
        public List<ASTNode> Children { get; set; } = new List<ASTNode>(2);

        public ASTNode Condition;
        public ASTIfStatement(ASTNode condition) => Condition = condition;
    }

    public class ASTShortCircuit : ASTNode
    {
        public ASTNode.StatementKind Kind { get; set; } = ASTNode.StatementKind.ShortCircuit;
        public List<ASTNode> Children { get; set; }

        public ShortCircuit.ShortCircuitType ShortCircuitKind;
        public ASTShortCircuit(ShortCircuit.ShortCircuitType kind, List<ASTNode> conditions)
        {
            ShortCircuitKind = kind;
            Children = conditions;
        }
    }
}
