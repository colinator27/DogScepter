namespace DogScepterLib.Project.GML.Compiler
{
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
                default:
                    Node res = ParseAssignOrFunction(ctx);
                    if (res != null)
                        return res;
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

        private static Node ParseBlock(CodeContext ctx)
        {
            Node res = new(NodeKind.Block);

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
                        varNode = left;
                    else if (left.Kind == NodeKind.ChainReference)
                    {
                        // Find last variable reference in the chain
                        int i = left.Children.Count - 1;
                        do
                        {
                            varNode = left.Children[i];
                            i--;
                        }
                        while (i >= 0 && varNode.Kind != NodeKind.Variable);

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
            }

            if (left.Kind != NodeKind.FunctionCall && left.Kind != NodeKind.FunctionCallChain &&
                left.Kind != NodeKind.Prefix && left.Kind != NodeKind.Postfix)
                ctx.Error("Incomplete statement", curr);

            return left;
        }

        // Check for [low level, dot, variable, <opt. ++/-->], or [low level, dot, function call],
        // or [low level, open <for chain function calls>], or [low level, array open <for chain array accesses>]
        // Then does the check again, in a chain, if necessary
        private static Node ParseChain(CodeContext ctx)
        {
            Node left = ParseBase(ctx);
            if (left == null)
                return null;

            bool finishedChain = false;

            while (!finishedChain)
            {
                Token curr = ctx.Tokens[ctx.Position];
                switch (curr.Kind)
                {
                    case TokenKind.Dot:
                        {
                            if (left.Kind != NodeKind.ChainReference)
                            {
                                // Convert left side to a chain reference
                                Node chain = new(NodeKind.ChainReference);
                                chain.Children.Add(left);
                                left = chain;
                            }

                            ctx.Position++;
                            Node next = ParseBase(ctx);
                            if (next == null)
                                return null;
                            left.Children.Add(next);
                            if (next.Kind == NodeKind.FunctionCall)
                            {
                                // Wrap the chain with a new function call node
                                Node call = new(NodeKind.FunctionCallChain);
                                call.Children.Add(left);
                                left = call;
                            }
                        }
                        break;
                    case TokenKind.Open:
                        {
                            // Handle chain function calls, like a()()
                            Node call = new(NodeKind.FunctionCallChain);
                            call.Children.Add(left);
                            ParseCallArguments(ctx, call);
                            left = call;
                        }
                        break;
                    case TokenKind.ArrayOpen:
                    case TokenKind.ArrayListOpen:
                    case TokenKind.ArrayMapOpen:
                    case TokenKind.ArrayGridOpen:
                    case TokenKind.ArrayDirectOpen:
                    case TokenKind.ArrayStructOpen:
                        {
                            Node accessor = new(NodeKind.Accessor, curr);
                            if (left.Kind != NodeKind.ChainReference)
                            {
                                // Convert left side to a chain reference
                                Node chain = new(NodeKind.ChainReference);
                                chain.Children.Add(left);
                                left = chain;
                            }
                            left.Children.Add(accessor);

                            ctx.Position++;
                            accessor.Children.Add(ParseExpression(ctx));
                            if (ctx.Tokens[ctx.Position].Kind == TokenKind.Comma)
                            {
                                ctx.Position++;
                                if (curr.Kind == TokenKind.ArrayOpen && ctx.BaseContext.IsGMS23)
                                {
                                    // This uses pre-2.3 comma syntax; deal with them separately
                                    accessor = new(NodeKind.Accessor, curr);
                                    left.Children.Add(accessor);
                                }
                                else if (curr.Kind != TokenKind.ArrayOpen && curr.Kind != TokenKind.ArrayGridOpen)
                                    ctx.Error("Invalid accessor (only takes 1 argument, supplied 2)", curr);
                                accessor.Children.Add(ParseExpression(ctx));
                            }
                            ExpectToken(ctx, TokenKind.ArrayClose, "']'");
                        }
                        break;
                    case TokenKind.Increment:
                    case TokenKind.Decrement:
                        {
                            if (left.Kind != NodeKind.ChainReference && left.Kind != NodeKind.Variable &&
                                left.Kind != NodeKind.Accessor)
                                ctx.Error("Invalid postfix", curr);

                            ctx.Position++;
                            Node n = new(NodeKind.Postfix, curr);
                            n.Children.Add(left);
                            return n;
                        }
                    default:
                        finishedChain = true;
                        break;
                }
            }

            return left;
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
                    ctx.Position++;
                    return new Node(NodeKind.Variable, curr);
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
                        if (n2 != null && n2.Kind != NodeKind.ChainReference && 
                            n2.Kind != NodeKind.Variable && n2.Kind != NodeKind.Accessor)
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
                        Node n = new(NodeKind.FunctionCall, new Token(ctx, TokenKind.FunctionCall, -1) { Value = new TokenFunction("@@NewGMLArray@@", null) });
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
                // TODO: TokenKind.New:
                // TODO: TokenKind.Delete:
                // TODO: TokenKind.Try:
            }

            ctx.Error("Invalid base expression", curr);
            return null;
        }

        private static void ParseCallArguments(CodeContext ctx, Node parent)
        {
            ExpectToken(ctx, TokenKind.Open, "'('");

            Token curr = ctx.Tokens[ctx.Position];
            while (curr.Kind != TokenKind.EOF && curr.Kind != TokenKind.Close)
            {
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
                    res.Children.Add(ParseAnd(ctx));
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
                    res.Children.Add(ParseXor(ctx));
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
            Node res = new(NodeKind.FunctionCall, new Token(ctx, TokenKind.FunctionCall, -1) { Value = new TokenFunction("@@NewGMLObject@@", null) });
            Node decl = new(NodeKind.FunctionDecl, ctx.Tokens[ctx.Position++]);
            res.Children.Add(decl);
            decl.Children.Add(new Node(NodeKind.Variable,
                                        new Token(ctx, TokenKind.Variable, -1)
                                        {
                                            Value = new TokenVariable($"__struct__{++ctx.BaseContext.Project.DataHandle.Stats.LastStructID}", null),
                                            ID = -16 // static
                                        }));
            decl.Children.Add(new Node(NodeKind.Group)); // no arguments
            Node body = new(NodeKind.Block);
            decl.Children.Add(body);

            // Going into a new scope; need to make new locals/statics
            var outerLocalVars = ctx.LocalVars;
            var outerStaticVars = ctx.StaticVars;
            ctx.LocalVars = new();
            ctx.StaticVars = new();

            // Read variables, and add assignments to function body
            Token curr = ctx.Tokens[ctx.Position];
            int argCount = 0;
            Node makeNewArg(Node original)
            {
                res.Children.Add(original);
                Node newArg = new(NodeKind.Accessor);
                newArg.Children.Add(new Node(NodeKind.Variable, new Token(ctx, TokenKind.Variable, -1) { Value = new TokenVariable("argument", null) }));
                newArg.Children.Add(new Node(NodeKind.Constant, new Token(ctx, new TokenConstant((double)(argCount++)), -1)));
                return newArg;
            }
            while (curr.Kind != TokenKind.EOF && curr.Kind != TokenKind.End)
            {
                Node assign = new(NodeKind.Assign, new Token(ctx, TokenKind.Assign, -1));
                body.Children.Add(assign);

                Token name = ExpectToken(ctx, TokenKind.Variable, "variable name");
                if (name != null)
                {
                    name.ID = -1; // self
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

            // Create info for this declaration
            decl.Info = new NodeFunctionInfo(true, ctx.LocalVars, ctx.StaticVars, new());

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
                Node inheritance = new(NodeKind.Group);
                res.Children.Add(inheritance);
                curr = ExpectToken(ctx, TokenKind.FunctionCall, "constructor call");
                if (curr != null)
                {
                    inheritance.Children.Add(new Node(NodeKind.Variable,
                                                new Token(ctx, new TokenVariable((curr.Value as TokenFunction).Name, null), curr.Index)));
                }

                ParseCallArguments(ctx, inheritance);

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

            // Create info for this declaration
            res.Info = new NodeFunctionInfo(isConstructor, ctx.LocalVars, ctx.StaticVars, ctx.ArgumentVars)
            {
                InheritingIndex = inheriting ? 2 : -1,
                OptionalArgsIndex = optionalArgs ? (inheriting ? 3 : 2) : -1
            };

            // Parse actual block
            res.Children.Add(ParseBlock(ctx));

            // Restore local/static AND argument vars from outer scope
            ctx.LocalVars = outerLocalVars;
            ctx.StaticVars = outerStaticVars;
            ctx.ArgumentVars = outerArgumentVars;

            return res;
        }
    }
}
