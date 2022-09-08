using System.Collections.Generic;
using static DogScepterLib.Core.Models.GMCode.Bytecode.Instruction;

namespace DogScepterLib.Project.GML.Compiler;

public class Parser
{
    public static void SkipSemicolons(CodeContext ctx)
    {
        Token curr = ctx.Tokens[ctx.Position];
        while (curr.Kind == TokenKind.Semicolon)
            curr = ctx.Tokens[++ctx.Position];
    }

    public static Node ParseStatement(CodeContext ctx)
    {
        Token curr = ctx.Tokens[ctx.Position];

        // Parse different statement types
        switch (curr.Kind)
        {
            case TokenKind.Begin:
                return ParseBlock(ctx);
            case TokenKind.Function:
                if (ctx.BaseContext.IsGMS23)
                    return ParseFunctionDecl(ctx);
                ctx.Error("Cannot use function declarations in pre-GMS2.3 runtime", curr);
                return null;
            case TokenKind.Exit:
                ctx.Position++;
                return new Node(NodeKind.Exit, curr);
            case TokenKind.Break:
                ctx.Position++;
                return new Node(NodeKind.Break, curr);
            case TokenKind.Continue:
                ctx.Position++;
                return new Node(NodeKind.Continue, curr);
            case TokenKind.Return:
                {
                    Node res = new Node(NodeKind.Return, curr);
                    curr = ctx.Tokens[++ctx.Position];
                    if (curr.Kind != TokenKind.Semicolon && 
                        (curr.Kind < TokenKind._KeywordsBegin || curr.Kind > TokenKind._KeywordsEnd || curr.Kind == TokenKind.Function))
                        res.Children.Add(ParseExpression(ctx));
                    return res;
                }
            case TokenKind.Switch:
                return ParseSwitch(ctx);
            case TokenKind.Var:
                return ParseLocalVarDecl(ctx);
            case TokenKind.Globalvar:
                UnsupportedSyntax(ctx, curr);
                break;
            case TokenKind.With:
                {
                    Node res = new Node(NodeKind.With, curr);
                    ctx.Position++;
                    res.Children.Add(ParseExpression(ctx));
                    if (ctx.Tokens[ctx.Position].Kind == TokenKind.Do)
                        ctx.Position++;
                    res.Children.Add(ParseStatement(ctx));
                    return res;
                }
            case TokenKind.While:
                {
                    Node res = new Node(NodeKind.While, curr);
                    ctx.Position++;
                    res.Children.Add(ParseExpression(ctx));
                    if (ctx.Tokens[ctx.Position].Kind == TokenKind.Do)
                        ctx.Position++;
                    res.Children.Add(ParseStatement(ctx));
                    return res;
                }
            case TokenKind.Repeat:
                {
                    Node res = new Node(NodeKind.Repeat, curr);
                    ctx.Position++;
                    res.Children.Add(ParseExpression(ctx));
                    res.Children.Add(ParseStatement(ctx));
                    return res;
                }
            case TokenKind.Do:
                {
                    Node res = new Node(NodeKind.DoUntil, curr);
                    ctx.Position++;
                    res.Children.Add(ParseStatement(ctx));
                    SkipSemicolons(ctx);
                    ExpectToken(ctx, TokenKind.Until, "'until'");
                    res.Children.Add(ParseExpression(ctx));
                    return res;
                }
            case TokenKind.If:
                {
                    Node res = new Node(NodeKind.If, curr);
                    ctx.Position++;
                    res.Children.Add(ParseExpression(ctx));
                    if (ctx.Tokens[ctx.Position].Kind == TokenKind.Then)
                        ctx.Position++;
                    res.Children.Add(ParseStatement(ctx));
                    SkipSemicolons(ctx);
                    if (ctx.Tokens[ctx.Position].Kind == TokenKind.Else)
                    {
                        ctx.Position++;
                        res.Children.Add(ParseStatement(ctx));
                    }
                    return res;
                }
            case TokenKind.For:
                return ParseFor(ctx);
            case TokenKind.Static:
                if (ctx.BaseContext.IsGMS23)
                    return ParseStatic(ctx);
                ctx.Error("Cannot use static in pre-GMS2.3 runtime", curr);
                return null;
            case TokenKind.Enum:
                return ParseEnum(ctx);
            case TokenKind.Try:
                if (ctx.BaseContext.IsGMS23)
                    UnsupportedSyntax(ctx, curr); // TODO
                else
                    ctx.Error("Cannot use static in pre-GMS2.3 runtime", curr);
                return null;
            default:
                {
                    Node res = ParseAssignOrFunction(ctx);
                    if (res != null)
                        return res;
                }
                break;
        }

        // No statement detected, just progress
        Token tok = ctx.Tokens[ctx.Position];
        if (tok.Kind != TokenKind.EOF)
            ctx.Position++;
        ctx.Error("Invalid statement", tok);
        return null;
    }

    private static Token ExpectToken(CodeContext ctx, TokenKind expectedKind, string expected)
    {
        Token got = ctx.Tokens[ctx.Position++];
        if (got.Kind == expectedKind)
            return got;

        if (got.Kind == TokenKind.EOF)
            ctx.Position--; // Prevent array access exception
        ctx.Error($"Expected {expected}", got);
        return null;
    }

    private static void UnsupportedSyntax(CodeContext ctx, Token t)
    {
        ctx.Error("Unsupported syntax", t);
        ctx.Position++;
    }

    private static Node ParseBlock(CodeContext ctx, bool newFuncBegin = false)
    {
        Node res = new(NodeKind.Block);

        Node oldFuncBegin = ctx.FunctionBeginBlock;
        Node oldFuncStatic = ctx.FunctionStatic;
        if (newFuncBegin)
        {
            ctx.FunctionBeginBlock = res;
            ctx.FunctionStatic = null;
        }

        ctx.Position++;
        SkipSemicolons(ctx);
        Token curr = ctx.Tokens[ctx.Position];
        while (curr.Kind != TokenKind.EOF && curr.Kind != TokenKind.End)
        {
            res.Children.Add(ParseStatement(ctx));
            SkipSemicolons(ctx);
            curr = ctx.Tokens[ctx.Position];
        }
        ExpectToken(ctx, TokenKind.End, "'}'");

        if (newFuncBegin)
        {
            ctx.FunctionBeginBlock = oldFuncBegin;
            ctx.FunctionStatic = oldFuncStatic;
        }

        return res;
    }

    private static Node ParseAssignOrFunction(CodeContext ctx)
    {
        Node left = ParseChain(ctx);
        if (left == null)
            return null;

        Token curr = ctx.Tokens[ctx.Position];
        switch (curr.Kind)
        {
            case TokenKind.Assign:
            case TokenKind.AssignPlus:
            case TokenKind.AssignMinus:
            case TokenKind.AssignTimes:
            case TokenKind.AssignDivide:
            case TokenKind.AssignMod:
            case TokenKind.AssignAnd:
            case TokenKind.AssignOr:
            case TokenKind.AssignXor:
            case TokenKind.AssignNullCoalesce:
                Node varNode;
                if (left.Kind == NodeKind.Variable)
                {
                    varNode = left;
                }
                else if (left.Kind == NodeKind.ChainReference)
                {
                    varNode = left.Children[1];
                    if (varNode.Kind != NodeKind.Variable)
                    {
                        ctx.Error("Invalid variable chain in assignment", varNode.Token?.Index ?? -1);
                        break;
                    }
                }
                else
                {
                    ctx.Error("Invalid left-hand side of assignment", curr);
                    break;
                }

                // If this variable is a builtin, disallow assignment if read-only
                TokenVariable tokVar = (varNode.Token.Value as TokenVariable);
                if (tokVar.Builtin != null && !tokVar.Builtin.CanSet)
                    ctx.Error($"Invalid assignment of read-only variable '{tokVar.Name}'", varNode.Token);

                // Build actual assignment
                Node assign = new(NodeKind.Assign, curr);
                assign.Children.Add(left);
                ctx.Position++;
                assign.Children.Add(ParseExpression(ctx));
                return assign;
            case TokenKind.Increment:
            case TokenKind.Decrement:
                // This is a ++/-- after a chain, so this is just postfix
                Node postfix = new(NodeKind.Postfix, curr);
                postfix.Children.Add(left);
                ctx.Position++;
                return postfix;
        }

        if (left.Kind != NodeKind.FunctionCall && left.Kind != NodeKind.FunctionCallChain &&
            left.Kind != NodeKind.Prefix && left.Kind != NodeKind.Postfix)
        {
            if (left.Kind == NodeKind.ChainReference && left.Children[1].Kind == NodeKind.FunctionCall)
                return left;

            ctx.Error("Incomplete statement", curr);
        }

        return left;
    }

    private static Node ParseChain(CodeContext ctx, bool disallowCall = false, bool basic = false)
    {
        Node left = ParseBase(ctx);
        if (left == null)
            return null;

        do
        {
            // Check for actual chain references
            while (ctx.Tokens[ctx.Position].Kind == TokenKind.Dot)
            {
                Node chain = new(NodeKind.ChainReference);
                chain.Children.Add(left);
                left = chain;

                ctx.Position++;
                if (ctx.Tokens[ctx.Position].Kind == TokenKind.FunctionCall)
                {
                    Node func = new(NodeKind.FunctionCall, ctx.Tokens[ctx.Position]);
                    ctx.Position++;
                    ParseCallArguments(ctx, func);
                    // todo: need to do checks for accessors here
                    chain.Children.Add(func);
                }
                else
                {
                    chain.Children.Add(ParseVariable(ctx));
                }
            }

            // Check for function calls
            // todo
        }
        while (ctx.Tokens[ctx.Position].Kind == TokenKind.Dot);

        return left;
    }

    private static Node ParseVariable(CodeContext ctx)
    {
        Node res = new Node(NodeKind.Variable, ctx.Tokens[ctx.Position]);
        ctx.Position++;

        // Check for array indices
        Token curr = ctx.Tokens[ctx.Position];
        if (curr.Kind != TokenKind.EOF)
        {
            if (curr.Kind == TokenKind.ArrayOpen || curr.Kind == TokenKind.ArrayListOpen || curr.Kind == TokenKind.ArrayMapOpen ||
                curr.Kind == TokenKind.ArrayGridOpen || curr.Kind == TokenKind.ArrayDirectOpen || curr.Kind == TokenKind.ArrayStructOpen)
            {
                do
                {
                    ctx.Position++;

                    // Find accessor definition, add new node
                    var info = NodeAccessorInfo.Accessors[curr.Kind];
                    Node accessor = new Node(NodeKind.Accessor) { Info = info };
                    res.Children.Add(accessor);

                    // Parse expression(s), verify them
                    Node expr = ParseExpression(ctx);
                    accessor.Children.Add(expr);
                    if (info.DisallowStrings && expr.Kind == NodeKind.Constant && (expr.Token.Value as TokenConstant).Kind == ConstantKind.String)
                        ctx.Error("String used in invalid accessor context", expr.Token);

                    curr = ctx.Tokens[ctx.Position];
                    if (curr.Kind == TokenKind.Comma && (info.Kind == TokenKind.ArrayOpen || info.Kind == TokenKind.ArrayGridOpen))
                    {
                        ctx.Position++;
                        Node secondExpr = ParseExpression(ctx);
                        if (info.Kind == TokenKind.ArrayOpen)
                        {
                            // Parse second argument if normal array
                            Node secondAccessor = new Node(NodeKind.Accessor) { Info = info };
                            res.Children.Add(secondAccessor);
                            secondAccessor.Children.Add(secondExpr);
                        }
                        else
                        {
                            // Parse second argument if grid
                            ctx.Position++;
                            accessor.Children.Add(secondExpr);
                        }

                        // Also verify that argument isn't a string
                        if (info.DisallowStrings && secondExpr.Kind == NodeKind.Constant && (secondExpr.Token.Value as TokenConstant).Kind == ConstantKind.String)
                            ctx.Error("String used in invalid accessor context", secondExpr.Token);
                    }

                    // Advance past array close token
                    if (info.Kind == TokenKind.ArrayOpen || info.Kind == TokenKind.ArrayGridOpen)
                        ExpectToken(ctx, TokenKind.ArrayClose, "']' or ','");
                    else
                        ExpectToken(ctx, TokenKind.ArrayClose, "']'");
                    curr = ctx.Tokens[ctx.Position];
                }
                while (curr.Kind == TokenKind.ArrayOpen || curr.Kind == TokenKind.ArrayListOpen || curr.Kind == TokenKind.ArrayMapOpen ||
                       curr.Kind == TokenKind.ArrayGridOpen || curr.Kind == TokenKind.ArrayDirectOpen || curr.Kind == TokenKind.ArrayStructOpen);
            }
        }

        return res;
    }

    private static Node ParseBase(CodeContext ctx)
    {
        Token curr = ctx.Tokens[ctx.Position];

        switch (curr.Kind)
        {
            case TokenKind.Constant:
                ctx.Position++;
                return new Node(NodeKind.Constant, curr);
            case TokenKind.FunctionCall:
                {
                    Node n = new(NodeKind.FunctionCall, curr);
                    ctx.Position++;
                    ParseCallArguments(ctx, n);
                    return n;
                }
            case TokenKind.Variable:
                {
                    Node n = ParseVariable(ctx);
                    curr = ctx.Tokens[ctx.Position];
                    if (curr.Kind == TokenKind.Increment || curr.Kind == TokenKind.Decrement)
                    {
                        ctx.Position++;
                        Node postfix = new(NodeKind.Postfix, curr);
                        postfix.Children.Add(n);
                        return postfix;
                    }
                    return n;
                }
            case TokenKind.Open:
                {
                    ctx.Position++;
                    Node n = ParseExpression(ctx);
                    ExpectToken(ctx, TokenKind.Close, "')'");
                    return n;
                }
            case TokenKind.Increment:
            case TokenKind.Decrement:
                {
                    ctx.Position++;
                    Node n = new(NodeKind.Prefix, curr);
                    Node n2 = ParseChain(ctx);
                    n.Children.Add(n2);
                    if (n2 != null && n2.Kind != NodeKind.ChainReference && n2.Kind != NodeKind.Variable)
                        ctx.Error("Invalid prefix", curr);
                    return n;
                }
            case TokenKind.Not:
            case TokenKind.Plus:
            case TokenKind.Minus:
            case TokenKind.BitNegate:
                {
                    ctx.Position++;
                    Node n = new(NodeKind.Unary, curr);
                    n.Children.Add(ParseChain(ctx));
                    return n;
                }
            case TokenKind.ArrayOpen:
                if (ctx.BaseContext.IsGMS2)
                {
                    ctx.Position++;
                    Node n = new(NodeKind.FunctionCall, new Token(ctx, TokenKind.FunctionCall, -1) { Value = Builtins.MakeFuncToken(ctx, "@@NewGMLArray@@") });
                    curr = ctx.Tokens[ctx.Position];
                    while (curr.Kind != TokenKind.EOF && curr.Kind != TokenKind.ArrayClose)
                    {
                        n.Children.Add(ParseExpression(ctx));
                        curr = ctx.Tokens[ctx.Position];
                        if (curr.Kind == TokenKind.Comma)
                            curr = ctx.Tokens[++ctx.Position];
                        else if (curr.Kind != TokenKind.ArrayClose)
                        {
                            ctx.Error("Expected ',' or ']'", curr);
                            break;
                        }
                    }
                    ExpectToken(ctx, TokenKind.ArrayClose, "']'");
                    return n;
                }
                    
                ctx.Error("Cannot use array literal in pre-GMS2 runtime", curr);
                return null;
            case TokenKind.Begin:
                if (ctx.BaseContext.IsGMS23)
                    return ParseStruct(ctx);
                ctx.Error("Cannot use struct literal in pre-GMS2.3 runtime", curr);
                return null;
            case TokenKind.Function:
                if (ctx.BaseContext.IsGMS23)
                    return ParseFunctionDecl(ctx);
                ctx.Error("Cannot use function declarations in pre-GMS2.3 runtime", curr);
                return null;
            case TokenKind.New:
                if (ctx.BaseContext.IsGMS23)
                    return ParseNew(ctx);
                ctx.Error("Cannot use 'new' in pre-GMS2.3 runtime", curr);
                return null;
            case TokenKind.Delete:
                if (ctx.BaseContext.IsGMS23)
                {
                    ctx.Position++;
                    Node n = new(NodeKind.Assign, new Token(ctx, TokenKind.Assign, curr.Index));
                    n.Children.Add(ParseChain(ctx));
                    n.Children.Add(new Node(ctx, ctx.BaseContext.Builtins.VarGlobal["undefined"]));
                    return n;
                }
                ctx.Error("Cannot use 'delete' in pre-GMS2.3 runtime", curr);
                return null;
        }

        ctx.Error("Invalid base expression", curr);
        ctx.Position++; // prevent infinite loop
        return null;
    }

    private static void ParseCallArguments(CodeContext ctx, Node parent)
    {
        Token open = ExpectToken(ctx, TokenKind.Open, "'('");

        int argCount = 0;

        Token curr = ctx.Tokens[ctx.Position];
        while (curr.Kind != TokenKind.EOF && curr.Kind != TokenKind.Close)
        {
            argCount++;

            if (curr.Kind == TokenKind.Comma)
            {
                // Automatically supply undefined as an argument
                parent.Children.Add(new Node(ctx, ctx.BaseContext.Builtins.VarGlobal["undefined"]));
            }
            else
                parent.Children.Add(ParseExpression(ctx));

            curr = ctx.Tokens[ctx.Position];
            if (curr.Kind == TokenKind.Comma)
                curr = ctx.Tokens[++ctx.Position];
            else if (curr.Kind != TokenKind.Close)
            {
                ctx.Error("Expected ',' or ')'", curr);
                break;
            }
        }

        if (argCount >= 128)
            ctx.Error("Too many arguments to function call", open?.Index ?? -1);

        ExpectToken(ctx, TokenKind.Close, "')'");
    }

    private static Node ParseExpression(CodeContext ctx) => ParseConditional(ctx);

    private static Node ParseConditional(CodeContext ctx)
    {
        Node left = ParseNullCoalesce(ctx);
        if (left == null)
            return null;
        if (ctx.Tokens[ctx.Position].Kind == TokenKind.Conditional)
        {
            ctx.Position++;
            Node res = new(NodeKind.Conditional);
            res.Children.Add(left);
            res.Children.Add(ParseNullCoalesce(ctx));
            ExpectToken(ctx, TokenKind.Colon, "':'");
            res.Children.Add(ParseNullCoalesce(ctx));
            return res;
        }
        return left;
    }

    private static Node ParseNullCoalesce(CodeContext ctx)
    {
        Node left = ParseOr(ctx);
        if (left == null)
            return null;
        if (ctx.Tokens[ctx.Position].Kind == TokenKind.NullCoalesce)
        {
            ctx.Position++;
            Node res = new(NodeKind.NullCoalesce);
            res.Children.Add(left);
            res.Children.Add(ParseOr(ctx));
            return res;
        }
        return left;
    }

    private static Node ParseOr(CodeContext ctx)
    {
        Node left = ParseAnd(ctx);
        if (left == null)
            return null;
        Token curr = ctx.Tokens[ctx.Position];
        if (curr.Kind == TokenKind.Or)
        {
            ctx.Position++;
            Node res = new(NodeKind.Binary, curr);
            res.Children.Add(left);
            res.Children.Add(ParseAnd(ctx));
            while (ctx.Tokens[ctx.Position].Kind == TokenKind.Or)
            {
                ctx.Position++;
                res.Children.Add(ParseAnd(ctx));
            }
            return res;
        }
        return left;
    }

    private static Node ParseAnd(CodeContext ctx)
    {
        Node left = ParseXor(ctx);
        if (left == null)
            return null;
        Token curr = ctx.Tokens[ctx.Position];
        if (curr.Kind == TokenKind.And)
        {
            ctx.Position++;
            Node res = new(NodeKind.Binary, curr);
            res.Children.Add(left);
            res.Children.Add(ParseXor(ctx));
            while (ctx.Tokens[ctx.Position].Kind == TokenKind.And)
            {
                ctx.Position++;
                res.Children.Add(ParseXor(ctx));
            }
            return res;
        }
        return left;
    }

    private static Node ParseXor(CodeContext ctx)
    {
        Node left = ParseCompare(ctx);
        if (left == null)
            return null;
        Token curr = ctx.Tokens[ctx.Position];
        if (curr.Kind == TokenKind.Xor)
        {
            ctx.Position++;
            Node res = new(NodeKind.Binary, curr);
            res.Children.Add(left);
            res.Children.Add(ParseCompare(ctx));
            return res;
        }
        return left;
    }

    private static Node ParseCompare(CodeContext ctx)
    {
        Node left = ParseBitwise(ctx);
        if (left == null)
            return null;
        Token curr = ctx.Tokens[ctx.Position];
        switch (curr.Kind)
        {
            case TokenKind.Equal:
            case TokenKind.NotEqual:
            case TokenKind.Greater:
            case TokenKind.GreaterEqual:
            case TokenKind.Lesser:
            case TokenKind.LesserEqual:
                {
                    ctx.Position++;
                    Node res = new(NodeKind.Binary, curr);
                    res.Children.Add(left);
                    res.Children.Add(ParseBitwise(ctx));
                    return res;
                }
            case TokenKind.Assign:
                {
                    // Legacy: convert = to ==
                    ctx.Position++;
                    Node res = new(NodeKind.Binary, new Token(ctx, TokenKind.Equal, -1));
                    res.Children.Add(left);
                    res.Children.Add(ParseBitwise(ctx));
                    return res;
                }
        }
        return left;
    }

    private static Node ParseBitwise(CodeContext ctx)
    {
        Node left = ParseBitShift(ctx);
        if (left == null)
            return null;
        Token curr = ctx.Tokens[ctx.Position];
        switch (curr.Kind)
        {
            case TokenKind.BitAnd:
            case TokenKind.BitOr:
            case TokenKind.BitXor:
                Node res = new(NodeKind.Binary, curr);
                res.Children.Add(left);
                ctx.Position++;
                res.Children.Add(ParseBitShift(ctx));

                curr = ctx.Tokens[ctx.Position];
                while (curr.Kind == TokenKind.BitAnd ||
                        curr.Kind == TokenKind.BitOr ||
                        curr.Kind == TokenKind.BitXor)
                {
                    ctx.Position++;

                    Node next = new(NodeKind.Binary, curr);
                    next.Children.Add(res);
                    next.Children.Add(ParseBitShift(ctx));
                    res = next;

                    curr = ctx.Tokens[ctx.Position];
                }
                return res;
        }
        return left;
    }

    private static Node ParseBitShift(CodeContext ctx)
    {
        Node left = ParseAddSub(ctx);
        if (left == null)
            return null;
        Token curr = ctx.Tokens[ctx.Position];
        switch (curr.Kind)
        {
            case TokenKind.BitShiftLeft:
            case TokenKind.BitShiftRight:
                Node res = new(NodeKind.Binary, curr);
                res.Children.Add(left);
                ctx.Position++;
                res.Children.Add(ParseAddSub(ctx));

                curr = ctx.Tokens[ctx.Position];
                while (curr.Kind == TokenKind.BitShiftLeft ||
                        curr.Kind == TokenKind.BitShiftRight)
                {
                    ctx.Position++;

                    Node next = new(NodeKind.Binary, curr);
                    next.Children.Add(res);
                    next.Children.Add(ParseAddSub(ctx));
                    res = next;

                    curr = ctx.Tokens[ctx.Position];
                }
                return res;
        }
        return left;
    }

    private static Node ParseAddSub(CodeContext ctx)
    {
        Node left = ParseMulDiv(ctx);
        if (left == null)
            return null;
        Token curr = ctx.Tokens[ctx.Position];
        switch (curr.Kind)
        {
            case TokenKind.Plus:
            case TokenKind.Minus:
                Node res = new(NodeKind.Binary, curr);
                res.Children.Add(left);
                ctx.Position++;
                res.Children.Add(ParseMulDiv(ctx));

                curr = ctx.Tokens[ctx.Position];
                while (curr.Kind == TokenKind.Plus ||
                        curr.Kind == TokenKind.Minus)
                {
                    ctx.Position++;

                    Node next = new(NodeKind.Binary, curr);
                    next.Children.Add(res);
                    next.Children.Add(ParseMulDiv(ctx));
                    res = next;

                    curr = ctx.Tokens[ctx.Position];
                }
                return res;
        }
        return left;
    }

    private static Node ParseMulDiv(CodeContext ctx)
    {
        Node left = ParseChain(ctx);
        if (left == null)
            return null;
        Token curr = ctx.Tokens[ctx.Position];
        switch (curr.Kind)
        {
            case TokenKind.Times:
            case TokenKind.Divide:
            case TokenKind.Mod:
            case TokenKind.Div:
                Node res = new(NodeKind.Binary, curr);
                res.Children.Add(left);
                ctx.Position++;
                res.Children.Add(ParseChain(ctx));

                curr = ctx.Tokens[ctx.Position];
                while (curr.Kind == TokenKind.Times ||
                        curr.Kind == TokenKind.Divide ||
                        curr.Kind == TokenKind.Mod ||
                        curr.Kind == TokenKind.Div)
                {
                    ctx.Position++;

                    Node next = new(NodeKind.Binary, curr);
                    next.Children.Add(res);
                    next.Children.Add(ParseChain(ctx));
                    res = next;

                    curr = ctx.Tokens[ctx.Position];
                }
                return res;
        }
        return left;
    }

    private static Node ParseStruct(CodeContext ctx)
    {
        Node res = new(NodeKind.FunctionCall, new Token(ctx, TokenKind.FunctionCall, -1) { Value = Builtins.MakeFuncToken(ctx, "@@NewGMLObject@@") });
        Node decl = new(NodeKind.FunctionDecl, ctx.Tokens[ctx.Position++]);
        res.Children.Add(decl);
        string structFuncName = $"___struct___{++ctx.BaseContext.Project.DataHandle.Stats.LastStructID}";
        decl.Children.Add(new Node(NodeKind.Variable,
                                    new Token(ctx, TokenKind.Variable, -1)
                                    {
                                        Value = new TokenVariable(structFuncName, null)
                                        {
                                            InstanceType = (int)InstanceType.Static
                                        }
                                    }));
        decl.Children.Add(new Node(NodeKind.Group)); // no arguments
        Node body = new(NodeKind.Block);
        decl.Children.Add(body);

        // Going into a new scope; need to make new locals/statics
        var outerLocalVars = ctx.LocalVars;
        var outerStaticVars = ctx.StaticVars;
        ctx.LocalVars = new();
        ctx.StaticVars = new();

        FunctionReference reference = new FunctionReference(ctx.BaseContext, $"gml_Script_{structFuncName}_{ctx.CurrentName}", true);
        string prevName = ctx.CurrentName;
        ctx.CurrentName = structFuncName + "_" + ctx.CurrentName;

        // Read variables, and add assignments to function body
        Token curr = ctx.Tokens[ctx.Position];
        int argCount = 0;
        Node makeNewArg(Node original)
        {
            res.Children.Add(original);
            Node newArg = new(NodeKind.Variable, new Token(ctx, TokenKind.Variable, -1) { Value = new TokenVariable("argument", null) });
            Node accessor = new(NodeAccessorInfo.Accessors[TokenKind.ArrayOpen]);
            newArg.Children.Add(accessor);
            accessor.Children.Add(new Node(NodeKind.Constant, new Token(ctx, new TokenConstant((double)(argCount++)), -1)));
            return null;
        }
        while (curr.Kind != TokenKind.EOF && curr.Kind != TokenKind.End)
        {
            Node assign = new(NodeKind.Assign, new Token(ctx, TokenKind.Assign, -1));
            body.Children.Add(assign);

            Token name = ExpectToken(ctx, TokenKind.Variable, "variable name");
            if (name != null)
            {
                (name.Value as TokenVariable).InstanceType = (int)InstanceType.Self;
                assign.Children.Add(new Node(NodeKind.Variable, name));
            }

            ExpectToken(ctx, TokenKind.Colon, "':'");

            Node value = ParseExpression(ctx);

            // If the value isn't constant, the evaluation of the value needs to move outside the struct initialization
            if (value.Kind != NodeKind.Constant)
            {
                if (value.Kind == NodeKind.FunctionCall)
                {
                    // Handle recursive expression evaluation
                    string funcName = (value.Token.Value as TokenFunction).Name;
                    if (funcName == "@@NewGMLArray@@" || funcName == "@@NewGMLObject@@")
                    {
                        for (int i = 0; i < value.Children.Count; i++)
                        {
                            if (value.Kind != NodeKind.Constant && value.Kind != NodeKind.FunctionDecl)
                                value.Children[i] = makeNewArg(value.Children[i]);
                        }
                    }
                }
                else
                {
                    // Basic value move
                    value = makeNewArg(value);
                }
            }

            assign.Children.Add(value);

            // Move on to next element
            curr = ctx.Tokens[ctx.Position];
            if (curr.Kind == TokenKind.Comma)
                curr = ctx.Tokens[++ctx.Position];
            else if (curr.Kind != TokenKind.End)
            {
                ctx.Error("Expected ',' or '}'", curr);
                break;
            }
        }

        ExpectToken(ctx, TokenKind.End, "'}'");

        ctx.CurrentName = prevName;

        // Create info for this declaration
        decl.Info = new NodeFunctionInfo(reference, true, ctx.LocalVars, ctx.StaticVars, new());

        // Restore local/static vars from outer scope
        ctx.LocalVars = outerLocalVars;
        ctx.StaticVars = outerStaticVars;

        return res;
    }

    private static Node ParseFunctionDecl(CodeContext ctx)
    {
        Node res = new(NodeKind.FunctionDecl, ctx.Tokens[ctx.Position++]);

        // Check if this has a name, or if it's anonymous
        Token curr = ctx.Tokens[ctx.Position];
        switch (curr.Kind)
        {
            case TokenKind.FunctionCall:
                ctx.Position++;
                res.Children.Add(new Node(NodeKind.Variable,
                                    new Token(ctx, new TokenVariable((curr.Value as TokenFunction).Name, null), curr.Index)));
                break;
            case TokenKind.Variable:
                ctx.Position++;
                res.Children.Add(new Node(NodeKind.Variable, curr));
                break;
            default:
                // Should be anonymous
                res.Children.Add(new Node(NodeKind.Empty));
                break;
        }

        ExpectToken(ctx, TokenKind.Open, "'('");

        // Going into a new scope; need to make new locals/statics AND arguments
        var outerLocalVars = ctx.LocalVars;
        var outerStaticVars = ctx.StaticVars;
        var outerArgumentVars = ctx.ArgumentVars;
        ctx.LocalVars = new();
        ctx.StaticVars = new();
        ctx.ArgumentVars = new();

        // Parse arguments
        Node arguments = new(NodeKind.Group);
        res.Children.Add(arguments);
        curr = ctx.Tokens[ctx.Position];
        bool optionalArgs = false;
        while (curr.Kind != TokenKind.EOF && curr.Kind != TokenKind.Close)
        {
            Token variable = ExpectToken(ctx, TokenKind.Variable, "variable name");
            if (variable == null)
                break;
            Node variableNode = new Node(NodeKind.Variable, variable);
            arguments.Children.Add(variableNode);

            string variableName = (variable.Value as TokenVariable).Name;
            if (ctx.ArgumentVars.Contains(variableName))
                ctx.Error($"Used variable name '{variableName}' more than once in function declaration", variable);
            else
                ctx.ArgumentVars.Add(variableName);

            curr = ctx.Tokens[ctx.Position];
            if (curr.Kind == TokenKind.Assign)
            {
                // Default argument
                optionalArgs = true;
                ctx.Position++;
                variableNode.Children.Add(ParseExpression(ctx));
            }

            curr = ctx.Tokens[ctx.Position];
            if (curr.Kind == TokenKind.Comma)
                curr = ctx.Tokens[++ctx.Position];
            else if (curr.Kind != TokenKind.Close)
            {
                ctx.Error("Expected ',' or ')'", curr);
                break;
            }
        }
        ExpectToken(ctx, TokenKind.Close, "')'");

        if (arguments.Children.Count > 16)
            ctx.Error("Over 16 named arguments used in function declaration", res.Token);

        // Check for inheritance
        curr = ctx.Tokens[ctx.Position];
        bool inheriting = false;
        if (curr.Kind == TokenKind.Colon)
        {
            inheriting = true;

            ctx.Position++;
            curr = ExpectToken(ctx, TokenKind.FunctionCall, "constructor call");
            if (curr != null)
            {
                Node inheritedFunc = new Node(NodeKind.Variable,
                                              new Token(ctx, new TokenVariable((curr.Value as TokenFunction).Name, null), curr.Index));
                res.Children.Add(inheritedFunc);
                ParseCallArguments(ctx, inheritedFunc);
            }

            curr = ctx.Tokens[ctx.Position];
        }

        // Check for constructor
        bool isConstructor = false;
        while (curr.Kind == TokenKind.Variable)
        {
            if ((curr.Value as TokenVariable).Name == "constructor")
            {
                isConstructor = true;
            }
            else
                ctx.Error($"Unknown function attribute '{(curr.Value as TokenVariable).Name}'", curr);

            curr = ctx.Tokens[++ctx.Position];
            if (curr.Kind == TokenKind.Comma)
                curr = ctx.Tokens[++ctx.Position];
        }
        if (inheriting && !isConstructor)
            ctx.Error("Only constructor functions can inherit", res.Token);

        // Create optional argument code block if necessary
        if (optionalArgs)
        {
            Node optionalBlock = new(NodeKind.Block);
            res.Children.Add(optionalBlock);
            foreach (var arg in arguments.Children)
            {
                if (arg.Children.Count != 0)
                {
                    Node ifStatement = new(NodeKind.If);
                    optionalBlock.Children.Add(ifStatement);

                    Node binary = new(NodeKind.Binary, new Token(ctx, TokenKind.Equal, -1));
                    ifStatement.Children.Add(binary);
                    binary.Children.Add(new Node(NodeKind.Variable, arg.Token));
                    binary.Children.Add(new Node(ctx, ctx.BaseContext.Builtins.VarGlobal["undefined"]));

                    Node assign = new(NodeKind.Assign, new Token(ctx, TokenKind.Assign, -1));
                    ifStatement.Children.Add(assign);
                    assign.Children.Add(new Node(NodeKind.Variable, arg.Token));
                    assign.Children.Add(arg.Children[0]);
                    arg.Children.Clear();
                }
            }
        }

        FunctionReference reference;
        string name;
        bool anonymous;
        if (res.Children[0].Kind == NodeKind.Variable)
        {
            name = (res.Children[0].Token.Value as TokenVariable).Name;
            anonymous = false;
        }
        else
        {
            name = $"anon_{res.Token.Index}";
            anonymous = true;
        }
        if (ctx.IsScript && !anonymous)
        {
            // Add named function declaration to global scope if this is in a script
            if (ctx.Mode != CodeContext.CodeMode.ReplaceFunctions && ctx.BaseContext.Functions.TryGetValue(name, out reference))
            {
                ctx.Error($"Redefining function '{name}'", res.Children[0].Token);
            }
            else
            {
                reference = new FunctionReference(ctx.BaseContext, $"gml_Script_{name}", false);
                ctx.BaseContext.Functions[name] = reference;
            }
        }
        else
        {
            // This isn't in a script (or is anonymous), so we're not in global scope--but we should make the reference anyway, for later (and to add to data)
            reference = new FunctionReference(ctx.BaseContext, $"gml_Script_{name}_{ctx.CurrentName}", anonymous);
        }

        // Create info for this declaration
        res.Info = new NodeFunctionInfo(reference, isConstructor, ctx.LocalVars, ctx.StaticVars, ctx.ArgumentVars)
        {
            InheritingIndex = inheriting ? 2 : -1,
            OptionalArgsIndex = optionalArgs ? (inheriting ? 3 : 2) : -1
        };

        // Parse actual block
        string prevName = ctx.CurrentName;
        ctx.CurrentName = name + "_" + ctx.CurrentName;
        res.Children.Add(ParseBlock(ctx, true));
        ctx.CurrentName = prevName;

        // Restore local/static AND argument vars from outer scope
        ctx.LocalVars = outerLocalVars;
        ctx.StaticVars = outerStaticVars;
        ctx.ArgumentVars = outerArgumentVars;

        return res;
    }

    private static Node ParseLocalVarDecl(CodeContext ctx)
    {
        Node res = new(NodeKind.LocalVarDecl, ctx.Tokens[ctx.Position++]);

        Token curr = ctx.Tokens[ctx.Position];
        while (curr.Kind == TokenKind.Variable)
        {
            TokenVariable tokenVar = (curr.Value as TokenVariable);
            tokenVar.InstanceType = (int)InstanceType.Local;
            if (tokenVar.Builtin != null)
                ctx.Error($"Local variable declared over builtin variable '{tokenVar.Name}'", curr);
            Node variable = new(NodeKind.Variable, curr);
            res.Children.Add(variable);

            // Add to this context's local list
            if (!ctx.LocalVars.Contains(tokenVar.Name))
                ctx.LocalVars.Add(tokenVar.Name);

            curr = ctx.Tokens[++ctx.Position];
            if (curr.Kind == TokenKind.Assign)
            {
                // Parse initial value
                ctx.Position++;
                variable.Children.Add(ParseExpression(ctx));
                curr = ctx.Tokens[ctx.Position];
            }

            if (curr.Kind != TokenKind.Comma)
                break;
            ctx.Position++;
        }

        // This error doesn't really occur "officially" but nobody should do this on purpose
        if (res.Children.Count == 0)
            ctx.Error("Local variable declaration has no variables", res.Token);

        return res;
    }

    private static Node ParseSwitch(CodeContext ctx)
    {
        Node res = new(NodeKind.Switch, ctx.Tokens[ctx.Position++]);

        res.Children.Add(ParseExpression(ctx));
        ExpectToken(ctx, TokenKind.Begin, "'{'");

        SkipSemicolons(ctx);
        Token curr = ctx.Tokens[ctx.Position];
        while (curr.Kind != TokenKind.EOF && curr.Kind != TokenKind.End)
        {
            if (curr.Kind == TokenKind.Case)
            {
                Node caseNode = new(NodeKind.SwitchCase, curr);
                ctx.Position++;
                caseNode.Children.Add(ParseExpression(ctx));
                ExpectToken(ctx, TokenKind.Colon, "':'");
                res.Children.Add(caseNode);
            }
            else if (curr.Kind == TokenKind.Default)
            {
                ctx.Position++;
                ExpectToken(ctx, TokenKind.Colon, "':'");
                res.Children.Add(new Node(NodeKind.SwitchDefault, curr));
            }
            else
                res.Children.Add(ParseStatement(ctx));
            SkipSemicolons(ctx);
            curr = ctx.Tokens[ctx.Position];
        }
        ExpectToken(ctx, TokenKind.End, "'}'");
            
        return res;
    }

    private static Node ParseFor(CodeContext ctx)
    {
        Node res = new(NodeKind.For, ctx.Tokens[ctx.Position++]);

        ExpectToken(ctx, TokenKind.Open, "'('");
            
        Token curr = ctx.Tokens[ctx.Position];
        if (curr.Kind == TokenKind.Semicolon)
        {
            // No initialization
            res.Children.Add(new Node(NodeKind.Block));
            ctx.Position++;
        }
        else
        {
            res.Children.Add(ParseStatement(ctx));
            SkipSemicolons(ctx);
        }

        curr = ctx.Tokens[ctx.Position];
        if (curr.Kind == TokenKind.Semicolon)
        {
            // true condition
            res.Children.Add(new Node(NodeKind.Constant, new Token(ctx, new TokenConstant(1), curr.Index)));
            ctx.Position++;
        }
        else
        {
            res.Children.Add(ParseExpression(ctx));
            if (ctx.Tokens[ctx.Position].Kind == TokenKind.Semicolon)
                ctx.Position++;
        }

        curr = ctx.Tokens[ctx.Position];
        if (curr.Kind == TokenKind.Close)
        {
            // No iteration statement
            res.Children.Add(new Node(NodeKind.Block));
            ctx.Position++;
        }
        else
        {
            res.Children.Add(ParseStatement(ctx));
            SkipSemicolons(ctx);
            ExpectToken(ctx, TokenKind.Close, "')'");
        }

        // Body
        res.Children.Add(ParseStatement(ctx));

        return res;
    }

    private static Node ParseStatic(CodeContext ctx)
    {
        Token first = ctx.Tokens[ctx.Position++];
        if (ctx.FunctionBeginBlock == null)
        {
            ctx.Error("Cannot use static outside of function declaration", first);
            return new Node(NodeKind.Empty);
        }

        // Make new static block at beginning if necessary
        if (ctx.FunctionStatic == null)
        {
            ctx.FunctionStatic = new Node(NodeKind.Static);
            ctx.FunctionBeginBlock.Children.Insert(0, ctx.FunctionStatic);
        }

        Token curr = ctx.Tokens[ctx.Position];
        bool didAnything = false;
        while (curr.Kind == TokenKind.Variable)
        {
            didAnything = true;

            TokenVariable tokenVar = (curr.Value as TokenVariable);
            if (tokenVar.Builtin != null)
                ctx.Error($"Static variable declared over builtin variable '{tokenVar.Name}'", curr);
            Node variable = new(NodeKind.Variable, curr);
            ctx.FunctionStatic.Children.Add(variable);

            // Add to this context's static list
            if (!ctx.StaticVars.Contains(tokenVar.Name))
                ctx.StaticVars.Add(tokenVar.Name);

            curr = ctx.Tokens[++ctx.Position];
            if (curr.Kind == TokenKind.Assign)
            {
                // Parse initial value
                ctx.Position++;
                variable.Children.Add(ParseExpression(ctx));
                curr = ctx.Tokens[ctx.Position];
            }
            else
                ctx.Error("Static declarations require an initial assignment", curr);

            if (curr.Kind != TokenKind.Comma)
                break;
            ctx.Position++;
        }

        // This error doesn't really occur "officially" but nobody should do this on purpose
        if (!didAnything)
            ctx.Error("Static variable declaration has no variables", first);

        // Don't need to insert any statements *here*
        return new Node(NodeKind.Empty);
    }

    private static Node ParseNew(CodeContext ctx)
    {
        Node res = new(NodeKind.New, ctx.Tokens[ctx.Position++]);

        Token curr = ctx.Tokens[ctx.Position];
        if (curr.Kind == TokenKind.FunctionCall)
        {
            res.Children.Add(new Node(NodeKind.Variable,
                                new Token(ctx, new TokenVariable((curr.Value as TokenFunction).Name, null), curr.Index)));
            ctx.Position++;
        }
        else
        {
            res.Children.Add(ParseChain(ctx, true, true)); // ignore calls when parsing chain here (otherwise it's ambiguous)
        }

        ParseCallArguments(ctx, res);

        return res;
    }

    private static Node ParseEnum(CodeContext ctx)
    {
        ctx.Position++;

        // Read enum name and check for errors
        Token nameToken = ExpectToken(ctx, TokenKind.Variable, "enum name");
        if (nameToken == null)
            return new Node(NodeKind.Empty);
        TokenVariable nameVar = (nameToken.Value as TokenVariable);
        if (nameVar.Builtin != null)
        {
            ctx.Error($"Enum declared using builtin variable name '{nameVar.Name}'", nameToken);
            return new Node(NodeKind.Empty);
        }
        if (ctx.BaseContext.Enums.ContainsKey(nameVar.Name))
            ctx.Error($"Enum name '{nameVar.Name}' declared more than once", nameToken);

        // Add new entry to global enum dictionary
        Enum newEnum = new(nameVar.Name);
        ctx.BaseContext.Enums[newEnum.Name] = newEnum;

        // Parse enum values
        ExpectToken(ctx, TokenKind.Begin, "'{'");
        Token curr = ctx.Tokens[ctx.Position];
        while (curr.Kind != TokenKind.EOF && curr.Kind != TokenKind.End)
        {
            Token entry = ExpectToken(ctx, TokenKind.Variable, "enum entry name");
            curr = ctx.Tokens[ctx.Position];
            if (entry == null)
                continue;

            string entryName = (entry.Value as TokenVariable).Name;
            if (newEnum.Contains(entryName))
                ctx.Error($"Duplicate enum entry name '{entryName}'", entry);

            // Parse actual value, if supplied
            EnumValue val;
            if (curr.Kind == TokenKind.Assign)
            {
                ctx.Position++;
                val = new EnumValue(entryName, NodeProcessor.ProcessNode(ctx, ParseExpression(ctx)));
            }
            else
                val = new EnumValue(entryName, null);
            newEnum.Values.Add(val);

            curr = ctx.Tokens[ctx.Position];
            if (curr.Kind == TokenKind.Comma)
                curr = ctx.Tokens[++ctx.Position];
            else if (curr.Kind != TokenKind.End)
                ctx.Error("Expected ','", curr);
        }
        ExpectToken(ctx, TokenKind.End, "'}'");

        return new Node(NodeKind.Empty);
    }
}
