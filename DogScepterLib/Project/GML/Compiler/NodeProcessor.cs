using System;
using System.Globalization;
using System.Text;

namespace DogScepterLib.Project.GML.Compiler
{
    public static class NodeProcessor
    {
        public static Node ProcessNode(CompileContext ctx, Node n)
        {
            for (int i = 0; i < n.Children.Count; i++)
            {
                n.Children[i] = ProcessNode(ctx, n.Children[i]);
            }

            switch (n.Kind)
            {
                case NodeKind.ChainReference:
                    if (ctx.ResolveEnums)
                        n = ResolveEnum(ctx, n);
                    break;
                case NodeKind.Unary:
                    n = OptimizeUnary(ctx, n);
                    break;
                case NodeKind.Binary:
                    n = OptimizeBinary(ctx, n);
                    break;
                case NodeKind.If:
                    n = OptimizeIf(ctx, n);
                    break;
                case NodeKind.FunctionCall:
                    n = OptimizeIntrinsicCall(ctx, n);
                    break;
            }

            return n;
        }

        private static Node ResolveEnum(CompileContext ctx, Node n)
        {
            if (n.Children.Count != 2 || n.Children[0].Kind != NodeKind.Variable || n.Children[1].Kind != NodeKind.Variable)
                return n;

            // Try to find enum value (and replace with its number)
            string enumName = (n.Children[0].Token.Value as TokenVariable).Name;
            if (ctx.Enums.TryGetValue(enumName, out Enum baseEnum))
            {
                string valName = (n.Children[1].Token.Value as TokenVariable).Name;
                if (baseEnum.TryGetValue(valName, out EnumValue enumVal))
                {
                    if (ctx.ReferencedEnums.Count != 0)
                    {
                        // Currently resolving interdependent enums
                        if (ctx.ReferencedEnums.Add(enumName))
                        {
                            if (enumVal.HasValue)
                            {
                                return new Node(NodeKind.Constant, new Token(n.Children[0].Token.Context, new TokenConstant(enumVal.Value), -1));
                            }
                            else
                                n.Children[0].Token.Context.Error($"Too much enum reference complexity", n.Children[0].Token);
                        }
                        else
                            n.Children[0].Token.Context.Error($"Cross-referenced enums not supported", n.Children[0].Token);
                    }
                    else
                    {
                        // Not resolving interdependent enums, but can resolve some now
                        if (enumVal.HasValue)
                        {
                            return new Node(NodeKind.Constant, new Token(n.Children[0].Token.Context, new TokenConstant(enumVal.Value), -1));
                        }
                    }
                }
                else
                    n.Children[1].Token.Context.Error($"Nonexistent enum value '{valName}' in enum '{enumName}'", n.Children[1].Token);
            }

            return n;
        }

        private static Node OptimizeUnary(CompileContext ctx, Node n)
        {
            if (n.Children[0].Kind != NodeKind.Constant)
                return n;

            var constant = (n.Children[0].Token.Value as TokenConstant);

            switch (n.Token.Kind)
            {
                case TokenKind.Minus:
                    switch (constant.Kind)
                    {
                        case ConstantKind.Number:
                            constant.ValueNumber = -constant.ValueNumber;
                            return n.Children[0];
                        case ConstantKind.Int64:
                            constant.ValueInt64 = -constant.ValueInt64;
                            return n.Children[0];
                    }
                    break;
                case TokenKind.Not:
                    switch (constant.Kind)
                    {
                        case ConstantKind.Number:
                            constant.IsBool = true;
                            constant.ValueNumber = (constant.ValueNumber > 0.5) ? 0 : 1;
                            return n.Children[0];
                        case ConstantKind.Int64:
                            constant.Kind = ConstantKind.Number;
                            constant.IsBool = true;
                            constant.ValueNumber = (constant.ValueInt64 >= 1) ? 0 : 1;
                            return n.Children[0];
                    }
                    break;
                case TokenKind.BitNegate:
                    switch (constant.Kind)
                    {
                        case ConstantKind.Number:
                            constant.ValueNumber = ~(long)constant.ValueNumber;
                            return n.Children[0];
                        case ConstantKind.Int64:
                            constant.ValueInt64 = ~constant.ValueInt64;
                            return n.Children[0];
                    }
                    break;
            }

            return n;
        }

        private static Node OptimizeIf(CompileContext ctx, Node n)
        {
            if (n.Children[0].Kind != NodeKind.Constant)
                return n;

            TokenConstant constant = n.Children[0].Token.Value as TokenConstant;
            bool isTruthy;
            if (constant.Kind == ConstantKind.Number)
                isTruthy = constant.ValueNumber > 0.5;
            else if (constant.Kind == ConstantKind.Int64)
                isTruthy = constant.ValueInt64 >= 1;
            else
                return n;

            if (isTruthy)
            {
                // Optimize if (true) - replace with body
                return n.Children[1];
            }
            else
            {
                // Optimize if (false) - replace with else statement, or otherwise nothing
                if (n.Children.Count == 3)
                    return n.Children[2];
                return new Node(NodeKind.Empty);
            }
        }

        private static Node OptimizeIntrinsicCall(CompileContext ctx, Node n)
        {
            if (n.Children.Count != 1 || n.Children[0].Kind != NodeKind.Constant)
                return n;

            TokenFunction tokenFunc = n.Token.Value as TokenFunction;
            if (tokenFunc.Builtin == null)
                return n; // Not necessary to process this further, we know it can't be one of the ones below

            TokenConstant constant = n.Children[0].Token.Value as TokenConstant;

            switch (tokenFunc.Name)
            {
                case "ord":
                    if (constant.Kind == ConstantKind.String && constant.ValueString.Length != 0)
                    {
                        byte[] utf8 = Encoding.UTF8.GetBytes(constant.ValueString);
                        int number;
                        if ((utf8[0] & 0x80) != 0)
                        {
                            if ((utf8[0] & 0xF8) == 0xF0)
                            {
                                number = ((utf8[0] & 7) << 18) + ((utf8[1] & 63) << 12) + ((utf8[2] & 63) << 6) + (utf8[3] & 63);
                            }
                            else if ((utf8[0] & 0x20) != 0)
                            {
                                number = ((utf8[0] & 0xF) << 12) + ((utf8[1] & 0x3F) << 6) + (utf8[2] & 0x3F);
                            }
                            else
                            {
                                number = ((utf8[0] & 0x1F) << 6) + (utf8[1] & 0x3F);
                            }
                        }
                        else
                            number = utf8[0];
                        constant.Kind = ConstantKind.Number;
                        constant.ValueNumber = number;
                        return n.Children[0];
                    }
                    break;
                case "chr":
                    {
                        long number;
                        if (constant.Kind == ConstantKind.Number)
                            number = Math.Max(0, Convert.ToInt64(constant.ValueNumber));
                        else if (constant.Kind == ConstantKind.Int64)
                            number = Math.Max(0, constant.ValueInt64);
                        else
                            return n;

                        constant.Kind = ConstantKind.String;
                        try
                        {
                            constant.ValueString = char.ConvertFromUtf32((int)number);
                        }
                        catch (ArgumentOutOfRangeException)
                        {
                            constant.ValueString = "X";
                        }

                        return n.Children[0];
                    }
                case "int64":
                    {
                        if (constant.Kind == ConstantKind.Number)
                        {
                            constant.ValueInt64 = Convert.ToInt64(constant.ValueNumber);
                            constant.Kind = ConstantKind.Int64;
                        }
                        else if (constant.Kind != ConstantKind.Int64)
                            return n;

                        return n.Children[0];
                    }
                case "real":
                    {
                        switch (constant.Kind)
                        {
                            case ConstantKind.Number:
                                return n.Children[0];
                            case ConstantKind.Int64:
                                constant.Kind = ConstantKind.Number;
                                constant.ValueNumber = constant.ValueInt64;
                                return n.Children[0];
                            case ConstantKind.String:
                                {
                                    if (double.TryParse(constant.ValueString, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
                                    {
                                        constant.Kind = ConstantKind.Number;
                                        constant.ValueNumber = val;
                                        return n.Children[0];
                                    }

                                    // Normal double parsing didn't work, so try hex alternatively
                                    string hex = constant.ValueString.Trim();
                                    if (hex.StartsWith("0x") || hex.StartsWith("0X"))
                                    {
                                        if (int.TryParse(hex[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int val2))
                                        {
                                            constant.Kind = ConstantKind.Number;
                                            constant.ValueNumber = val2;
                                            return n.Children[0];
                                        }
                                    }

                                    n.Children[0].Token.Context.Error("Invalid real() argument", n.Children[0].Token);
                                    break;
                                }
                        }
                    }
                    break;
                case "string":
                    if (constant.Kind == ConstantKind.String)
                        return n.Children[0];
                    break;
            }

            return n;
        }

        private static Node OptimizeBinary(CompileContext ctx, Node n)
        {
            if (n.Children[0].Kind != NodeKind.Constant || n.Children[1].Kind != NodeKind.Constant)
                return n;

            do
            {
                TokenConstant left = n.Children[0].Token.Value as TokenConstant;
                TokenConstant right = n.Children[1].Token.Value as TokenConstant;

                bool didAnything = false;
                switch (n.Token.Kind)
                {
                    case TokenKind.Plus:
                        switch (left.Kind)
                        {
                            case ConstantKind.Number:
                                switch (right.Kind)
                                {
                                    case ConstantKind.Number:
                                        left.ValueNumber += right.ValueNumber;
                                        didAnything = true;
                                        break;
                                    case ConstantKind.Int64:
                                        left.Kind = ConstantKind.Int64;
                                        left.ValueInt64 = (long)left.ValueNumber + right.ValueInt64;
                                        didAnything = true;
                                        break;
                                }
                                break;
                            case ConstantKind.Int64:
                                switch (right.Kind)
                                {
                                    case ConstantKind.Number:
                                        left.ValueInt64 += (long)right.ValueNumber;
                                        didAnything = true;
                                        break;
                                    case ConstantKind.Int64:
                                        left.ValueInt64 += right.ValueInt64;
                                        didAnything = true;
                                        break;
                                }
                                break;
                        }
                        break;
                    case TokenKind.Minus:
                        switch (left.Kind)
                        {
                            case ConstantKind.Number:
                                switch (right.Kind)
                                {
                                    case ConstantKind.Number:
                                        left.ValueNumber -= right.ValueNumber;
                                        didAnything = true;
                                        break;
                                    case ConstantKind.Int64:
                                        left.Kind = ConstantKind.Int64;
                                        left.ValueInt64 = (long)left.ValueNumber - right.ValueInt64;
                                        didAnything = true;
                                        break;
                                }
                                break;
                            case ConstantKind.Int64:
                                switch (right.Kind)
                                {
                                    case ConstantKind.Number:
                                        left.ValueInt64 -= (long)right.ValueNumber;
                                        didAnything = true;
                                        break;
                                    case ConstantKind.Int64:
                                        left.ValueInt64 -= right.ValueInt64;
                                        didAnything = true;
                                        break;
                                }
                                break;
                        }
                        break;
                    case TokenKind.Times:
                        switch (left.Kind)
                        {
                            case ConstantKind.Number:
                                switch (right.Kind)
                                {
                                    case ConstantKind.Number:
                                        left.ValueNumber *= right.ValueNumber;
                                        didAnything = true;
                                        break;
                                    case ConstantKind.Int64:
                                        left.Kind = ConstantKind.Int64;
                                        left.ValueInt64 = (long)left.ValueNumber * right.ValueInt64;
                                        didAnything = true;
                                        break;
                                    case ConstantKind.String:
                                        // Little-known syntax (5 * "string") repeats it
                                        left.Kind = ConstantKind.String;
                                        left.ValueString = 
                                            new StringBuilder(right.ValueString.Length * (int)left.ValueNumber)
                                                .Insert(0, right.ValueString, (int)left.ValueNumber).ToString();
                                        break;
                                }
                                break;
                            case ConstantKind.Int64:
                                switch (right.Kind)
                                {
                                    case ConstantKind.Number:
                                        left.ValueInt64 *= (long)right.ValueNumber;
                                        didAnything = true;
                                        break;
                                    case ConstantKind.Int64:
                                        left.ValueInt64 *= right.ValueInt64;
                                        didAnything = true;
                                        break;
                                }
                                break;
                        }
                        break;
                    case TokenKind.Divide:
                        switch (left.Kind)
                        {
                            case ConstantKind.Number:
                                switch (right.Kind)
                                {
                                    case ConstantKind.Number:
                                        if (right.ValueNumber == 0)
                                        {
                                            n.Children[1].Token.Context.Error("Division by zero", n.Children[1].Token);
                                            break;
                                        }
                                        left.ValueNumber /= right.ValueNumber;
                                        didAnything = true;
                                        break;
                                    case ConstantKind.Int64:
                                        if (right.ValueInt64 == 0)
                                        {
                                            n.Children[1].Token.Context.Error("Division by zero", n.Children[1].Token);
                                            break;
                                        }
                                        left.Kind = ConstantKind.Int64;
                                        left.ValueInt64 = (long)left.ValueNumber / right.ValueInt64;
                                        didAnything = true;
                                        break;
                                }
                                break;
                            case ConstantKind.Int64:
                                switch (right.Kind)
                                {
                                    case ConstantKind.Number:
                                        if ((long)right.ValueNumber == 0)
                                        {
                                            n.Children[1].Token.Context.Error("Division by zero", n.Children[1].Token);
                                            break;
                                        }
                                        left.ValueInt64 /= (long)right.ValueNumber;
                                        didAnything = true;
                                        break;
                                    case ConstantKind.Int64:
                                        if (right.ValueInt64 == 0)
                                        {
                                            n.Children[1].Token.Context.Error("Division by zero", n.Children[1].Token);
                                            break;
                                        }
                                        left.ValueInt64 /= right.ValueInt64;
                                        didAnything = true;
                                        break;
                                }
                                break;
                        }
                        break;
                    case TokenKind.Div:
                        switch (left.Kind)
                        {
                            case ConstantKind.Number:
                                switch (right.Kind)
                                {
                                    case ConstantKind.Number:
                                        if ((int)right.ValueNumber == 0)
                                        {
                                            n.Children[1].Token.Context.Error("Division by zero", n.Children[1].Token);
                                            break;
                                        }
                                        left.ValueNumber = (long)left.ValueNumber / (long)right.ValueNumber;
                                        didAnything = true;
                                        break;
                                    case ConstantKind.Int64:
                                        if (right.ValueInt64 == 0)
                                        {
                                            n.Children[1].Token.Context.Error("Division by zero", n.Children[1].Token);
                                            break;
                                        }
                                        left.Kind = ConstantKind.Int64;
                                        left.ValueInt64 = (long)left.ValueNumber / right.ValueInt64;
                                        didAnything = true;
                                        break;
                                }
                                break;
                            case ConstantKind.Int64:
                                switch (right.Kind)
                                {
                                    case ConstantKind.Number:
                                        if ((long)right.ValueNumber == 0)
                                        {
                                            n.Children[1].Token.Context.Error("Division by zero", n.Children[1].Token);
                                            break;
                                        }
                                        left.ValueInt64 /= (long)right.ValueNumber;
                                        didAnything = true;
                                        break;
                                    case ConstantKind.Int64:
                                        if (right.ValueInt64 == 0)
                                        {
                                            n.Children[1].Token.Context.Error("Division by zero", n.Children[1].Token);
                                            break;
                                        }
                                        left.ValueInt64 /= right.ValueInt64;
                                        didAnything = true;
                                        break;
                                }
                                break;
                        }
                        break;
                    case TokenKind.Mod:
                        switch (left.Kind)
                        {
                            case ConstantKind.Number:
                                switch (right.Kind)
                                {
                                    case ConstantKind.Number:
                                        if (right.ValueNumber == 0)
                                        {
                                            n.Children[1].Token.Context.Error("Modulo by zero", n.Children[1].Token);
                                            break;
                                        }
                                        left.ValueNumber %= right.ValueNumber;
                                        didAnything = true;
                                        break;
                                    case ConstantKind.Int64:
                                        if (right.ValueInt64 == 0)
                                        {
                                            n.Children[1].Token.Context.Error("Modulo by zero", n.Children[1].Token);
                                            break;
                                        }
                                        left.Kind = ConstantKind.Int64;
                                        left.ValueInt64 = (long)left.ValueNumber % right.ValueInt64;
                                        didAnything = true;
                                        break;
                                }
                                break;
                            case ConstantKind.Int64:
                                switch (right.Kind)
                                {
                                    case ConstantKind.Number:
                                        if ((long)right.ValueNumber == 0)
                                        {
                                            n.Children[1].Token.Context.Error("Modulo by zero", n.Children[1].Token);
                                            break;
                                        }
                                        left.ValueInt64 %= (long)right.ValueNumber;
                                        didAnything = true;
                                        break;
                                    case ConstantKind.Int64:
                                        if (right.ValueInt64 == 0)
                                        {
                                            n.Children[1].Token.Context.Error("Modulo by zero", n.Children[1].Token);
                                            break;
                                        }
                                        left.ValueInt64 %= right.ValueInt64;
                                        didAnything = true;
                                        break;
                                }
                                break;
                        }
                        break;
                    case TokenKind.And:
                        switch (left.Kind)
                        {
                            case ConstantKind.Number:
                                switch (right.Kind)
                                {
                                    case ConstantKind.Number:
                                        left.ValueNumber = (left.ValueNumber > 0.5 && right.ValueNumber > 0.5) ? 1 : 0;
                                        didAnything = true;
                                        break;
                                    case ConstantKind.Int64:
                                        left.ValueNumber = (left.ValueNumber > 0.5 && right.ValueInt64 >= 1) ? 1 : 0;
                                        didAnything = true;
                                        break;
                                }
                                break;
                            case ConstantKind.Int64:
                                switch (right.Kind)
                                {
                                    case ConstantKind.Number:
                                        left.Kind = ConstantKind.Number;
                                        left.ValueNumber = (left.ValueInt64 >= 1 && right.ValueNumber > 0.5) ? 1 : 0;
                                        didAnything = true;
                                        break;
                                    case ConstantKind.Int64:
                                        left.Kind = ConstantKind.Number;
                                        left.ValueNumber = (left.ValueInt64 >= 1 && right.ValueInt64 >= 1) ? 1 : 0;
                                        didAnything = true;
                                        break;
                                }
                                break;
                        }
                        break;
                    case TokenKind.Or:
                        switch (left.Kind)
                        {
                            case ConstantKind.Number:
                                switch (right.Kind)
                                {
                                    case ConstantKind.Number:
                                        left.ValueNumber = (left.ValueNumber > 0.5 || right.ValueNumber > 0.5) ? 1 : 0;
                                        didAnything = true;
                                        break;
                                    case ConstantKind.Int64:
                                        left.ValueNumber = (left.ValueNumber > 0.5 || right.ValueInt64 >= 1) ? 1 : 0;
                                        didAnything = true;
                                        break;
                                }
                                break;
                            case ConstantKind.Int64:
                                switch (right.Kind)
                                {
                                    case ConstantKind.Number:
                                        left.Kind = ConstantKind.Number;
                                        left.ValueNumber = (left.ValueInt64 >= 1 || right.ValueNumber > 0.5) ? 1 : 0;
                                        didAnything = true;
                                        break;
                                    case ConstantKind.Int64:
                                        left.Kind = ConstantKind.Number;
                                        left.ValueNumber = (left.ValueInt64 >= 1 || right.ValueInt64 >= 1) ? 1 : 0;
                                        didAnything = true;
                                        break;
                                }
                                break;
                        }
                        break;
                    case TokenKind.Xor:
                        switch (left.Kind)
                        {
                            case ConstantKind.Number:
                                switch (right.Kind)
                                {
                                    case ConstantKind.Number:
                                        left.ValueNumber = (left.ValueNumber > 0.5 ? 1 : 0) ^ (right.ValueNumber > 0.5 ? 1 : 0);
                                        didAnything = true;
                                        break;
                                    case ConstantKind.Int64:
                                        left.ValueNumber = (left.ValueNumber > 0.5 ? 1 : 0) ^ (right.ValueInt64 >= 1 ? 1 : 0);
                                        didAnything = true;
                                        break;
                                }
                                break;
                            case ConstantKind.Int64:
                                switch (right.Kind)
                                {
                                    case ConstantKind.Number:
                                        left.Kind = ConstantKind.Number;
                                        left.ValueNumber = (left.ValueInt64 >= 1 ? 1 : 0) ^ (right.ValueNumber > 0.5 ? 1 : 0);
                                        didAnything = true;
                                        break;
                                    case ConstantKind.Int64:
                                        left.Kind = ConstantKind.Number;
                                        left.ValueNumber = (left.ValueInt64 >= 1 ? 1 : 0) ^ (right.ValueInt64 >= 1 ? 1 : 0);
                                        didAnything = true;
                                        break;
                                }
                                break;
                        }
                        break;
                    case TokenKind.BitOr:
                        switch (left.Kind)
                        {
                            case ConstantKind.Number:
                                switch (right.Kind)
                                {
                                    case ConstantKind.Number:
                                        left.ValueNumber = (long)left.ValueNumber | (long)right.ValueNumber;
                                        didAnything = true;
                                        break;
                                    case ConstantKind.Int64:
                                        left.Kind = ConstantKind.Int64;
                                        left.ValueInt64 = (long)left.ValueNumber | right.ValueInt64;
                                        didAnything = true;
                                        break;
                                }
                                break;
                            case ConstantKind.Int64:
                                switch (right.Kind)
                                {
                                    case ConstantKind.Number:
                                        left.ValueInt64 |= (long)right.ValueNumber;
                                        didAnything = true;
                                        break;
                                    case ConstantKind.Int64:
                                        left.ValueInt64 |= right.ValueInt64;
                                        didAnything = true;
                                        break;
                                }
                                break;
                        }
                        break;
                    case TokenKind.BitAnd:
                        switch (left.Kind)
                        {
                            case ConstantKind.Number:
                                switch (right.Kind)
                                {
                                    case ConstantKind.Number:
                                        left.ValueNumber = (long)left.ValueNumber & (long)right.ValueNumber;
                                        didAnything = true;
                                        break;
                                    case ConstantKind.Int64:
                                        left.Kind = ConstantKind.Int64;
                                        left.ValueInt64 = (long)left.ValueNumber & right.ValueInt64;
                                        didAnything = true;
                                        break;
                                }
                                break;
                            case ConstantKind.Int64:
                                switch (right.Kind)
                                {
                                    case ConstantKind.Number:
                                        left.ValueInt64 &= (long)right.ValueNumber;
                                        didAnything = true;
                                        break;
                                    case ConstantKind.Int64:
                                        left.ValueInt64 &= right.ValueInt64;
                                        didAnything = true;
                                        break;
                                }
                                break;
                        }
                        break;
                    case TokenKind.BitXor:
                        switch (left.Kind)
                        {
                            case ConstantKind.Number:
                                switch (right.Kind)
                                {
                                    case ConstantKind.Number:
                                        left.ValueNumber = (long)left.ValueNumber ^ (long)right.ValueNumber;
                                        didAnything = true;
                                        break;
                                    case ConstantKind.Int64:
                                        left.Kind = ConstantKind.Int64;
                                        left.ValueInt64 = (long)left.ValueNumber ^ right.ValueInt64;
                                        didAnything = true;
                                        break;
                                }
                                break;
                            case ConstantKind.Int64:
                                switch (right.Kind)
                                {
                                    case ConstantKind.Number:
                                        left.ValueInt64 ^= (long)right.ValueNumber;
                                        didAnything = true;
                                        break;
                                    case ConstantKind.Int64:
                                        left.ValueInt64 ^= right.ValueInt64;
                                        didAnything = true;
                                        break;
                                }
                                break;
                        }
                        break;
                    case TokenKind.BitShiftLeft:
                        switch (left.Kind)
                        {
                            case ConstantKind.Number:
                                switch (right.Kind)
                                {
                                    case ConstantKind.Number:
                                        if ((int)right.ValueNumber < 64)
                                            left.ValueNumber = (long)left.ValueNumber << (int)right.ValueNumber;
                                        else
                                            left.ValueNumber = 0;
                                        didAnything = true;
                                        break;
                                    case ConstantKind.Int64:
                                        if (right.ValueInt64 < 32)
                                            left.ValueNumber = (long)left.ValueNumber << (int)right.ValueInt64;
                                        else
                                            left.ValueNumber = 0;
                                        didAnything = true;
                                        break;
                                }
                                break;
                            case ConstantKind.Int64:
                                switch (right.Kind)
                                {
                                    case ConstantKind.Number:
                                        if ((int)right.ValueNumber < 64)
                                            left.ValueInt64 <<= (int)right.ValueNumber;
                                        else
                                            left.ValueInt64 = 0;
                                        didAnything = true;
                                        break;
                                    case ConstantKind.Int64:
                                        if ((int)right.ValueNumber < 64)
                                            left.ValueInt64 <<= (int)right.ValueInt64;
                                        else
                                            left.ValueInt64 = 0;
                                        didAnything = true;
                                        break;
                                }
                                break;
                        }
                        break;
                    case TokenKind.BitShiftRight:
                        switch (left.Kind)
                        {
                            case ConstantKind.Number:
                                switch (right.Kind)
                                {
                                    case ConstantKind.Number:
                                        if ((int)right.ValueNumber < 64)
                                            left.ValueNumber = (long)left.ValueNumber >> (int)right.ValueNumber;
                                        else
                                            left.ValueNumber = 0;
                                        didAnything = true;
                                        break;
                                    case ConstantKind.Int64:
                                        if (right.ValueInt64 < 32)
                                            left.ValueNumber = (long)left.ValueNumber >> (int)right.ValueInt64;
                                        else
                                            left.ValueNumber = 0;
                                        didAnything = true;
                                        break;
                                }
                                break;
                            case ConstantKind.Int64:
                                switch (right.Kind)
                                {
                                    case ConstantKind.Number:
                                        if ((int)right.ValueNumber < 64)
                                            left.ValueInt64 >>= (int)right.ValueNumber;
                                        else
                                            left.ValueInt64 = 0;
                                        didAnything = true;
                                        break;
                                    case ConstantKind.Int64:
                                        if ((int)right.ValueNumber < 64)
                                            left.ValueInt64 >>= (int)right.ValueInt64;
                                        else
                                            left.ValueInt64 = 0;
                                        didAnything = true;
                                        break;
                                }
                                break;
                        }
                        break;
                    case TokenKind.Equal:
                    case TokenKind.Greater:
                    case TokenKind.GreaterEqual:
                    case TokenKind.Lesser:
                    case TokenKind.LesserEqual:
                    case TokenKind.NotEqual:
                        double diff = 0;
                        switch (left.Kind)
                        {
                            case ConstantKind.Number:
                                switch (right.Kind)
                                {
                                    case ConstantKind.Number:
                                        diff = left.ValueNumber - right.ValueNumber;
                                        didAnything = true;
                                        break;
                                    case ConstantKind.Int64:
                                        diff = (long)left.ValueNumber - right.ValueInt64;
                                        didAnything = true;
                                        break;
                                }
                                break;
                            case ConstantKind.Int64:
                                switch (right.Kind)
                                {
                                    case ConstantKind.Number:
                                        diff = left.ValueInt64 - (long)right.ValueNumber;
                                        didAnything = true;
                                        break;
                                    case ConstantKind.Int64:
                                        diff = left.ValueInt64 - right.ValueInt64;
                                        didAnything = true;
                                        break;
                                }
                                break;
                            case ConstantKind.String:
                                if (right.Kind == ConstantKind.String)
                                {
                                    diff = string.Compare(left.ValueString, right.ValueString);
                                    didAnything = true;
                                }
                                break;
                        }

                        if (didAnything)
                        {
                            left.Kind = ConstantKind.Number;
                            left.IsBool = true;

                            switch (n.Token.Kind)
                            {
                                case TokenKind.Equal:
                                    left.ValueNumber = (diff == 0) ? 1 : 0;
                                    break;
                                case TokenKind.Greater:
                                    left.ValueNumber = (diff > 0) ? 1 : 0;
                                    break;
                                case TokenKind.GreaterEqual:
                                    left.ValueNumber = (diff >= 0) ? 1 : 0;
                                    break;
                                case TokenKind.Lesser:
                                    left.ValueNumber = (diff < 0) ? 1 : 0;
                                    break;
                                case TokenKind.LesserEqual:
                                    left.ValueNumber = (diff <= 0) ? 1 : 0;
                                    break;
                                case TokenKind.NotEqual:
                                    left.ValueNumber = (diff != 0) ? 1 : 0;
                                    break;
                            }
                        }
                        break;
                }

                if (didAnything)
                    n.Children.RemoveAt(1);
                else
                    break;
            }
            while (n.Children.Count >= 2 && n.Children[0].Kind == NodeKind.Constant && n.Children[1].Kind == NodeKind.Constant);

            // If fully optimized, there should only be one element left
            if (n.Children.Count == 1)
                return n.Children[0];

            return n;
        }
    }
}
