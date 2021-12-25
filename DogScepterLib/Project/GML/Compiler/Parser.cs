using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DogScepterLib.Project.GML.Compiler
{
    public class Parser
    {
        public static bool ExpectToken(CodeContext ctx, TokenKind expectedKind, string expected)
        {
            Token got = ctx.Tokens[ctx.Position++];
            if (got.Kind == expectedKind)
                return true;

            if (got.Kind == TokenKind.EOF)
                ctx.Position--; // Prevent array access exception
            ctx.Error($"Expected '{expected}'", got);
            return false;
        }

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

        public static Node ParseBlock(CodeContext ctx)
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
            ExpectToken(ctx, TokenKind.End, "}");

            return res;
        }

        public static Node ParseAssignOrFunction(CodeContext ctx)
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
                    if (left.Kind != NodeKind.Variable && left.Kind != NodeKind.ChainReference)
                    {
                        ctx.Error("Invalid assignment", curr);
                    }
                    break;
            }

            if (left.Kind != NodeKind.FunctionCall && left.Kind != NodeKind.FunctionCallChain &&
                left.Kind != NodeKind.Prefix && left.Kind != NodeKind.Postfix)
                ctx.Error("Incomplete statement", curr);

            return left;
        }

        // Check for [low level, dot, variable, <opt. ++/-->], or [low level, dot, function call],
        // or [low level, open <for chain function calls>], or [low level, array open <for chain array accesses>]
        // Then does the check again, in a chain, if necessary
        public static Node ParseChain(CodeContext ctx)
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
                            Node target = new(NodeKind.Accessor, curr);
                            if (left.Kind == NodeKind.ChainReference)
                            {
                                // Access applies to the last variable/function of chain
                                target.Children.Add(left.Children[^1]);
                                left.Children[^1] = target;
                            }
                            else
                            {
                                target.Children.Add(left);
                                left = target;
                            }

                            ctx.Position++;
                            target.Children.Add(ParseExpression(ctx));
                            if (ctx.Tokens[ctx.Position].Kind == TokenKind.Comma)
                            {
                                if (curr.Kind == TokenKind.ArrayOpen && ctx.BaseContext.IsGMS23)
                                {
                                    // This uses pre-2.3 comma syntax, so need to rewrite it for later
                                    // (make it nested, i.e. convert [1, 2] to [1][2])
                                    Node innerIndex = target.Children[1];
                                    ctx.Position++;
                                    target.Children[1] = ParseExpression(ctx);
                                    Node oldAccess = target.Children[0];
                                    target.Children[0] = new(NodeKind.Accessor, curr);
                                    target.Children[0].Children.Add(oldAccess);
                                    target.Children[0].Children.Add(innerIndex);
                                }
                                else
                                {
                                    if (curr.Kind != TokenKind.ArrayOpen && curr.Kind != TokenKind.ArrayGridOpen)
                                        ctx.Error("Invalid accessor (only takes 1 argument, supplied 2)", curr);
                                    ctx.Position++;
                                    target.Children.Add(ParseExpression(ctx));
                                }
                            }
                            ExpectToken(ctx, TokenKind.ArrayClose, "]");
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

        public static Node ParseBase(CodeContext ctx)
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
                        ExpectToken(ctx, TokenKind.Close, ")");
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
            }

            ctx.Error("Invalid base expression", curr);
            return null;
        }

        public static void ParseCallArguments(CodeContext ctx, Node parent)
        {
            ExpectToken(ctx, TokenKind.Open, "(");

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

            ExpectToken(ctx, TokenKind.Close, ")");
        }

        public static Node ParseExpression(CodeContext ctx)
        {
            // TODO: Handle operations, right now this is incomplete
            return ParseChain(ctx);
        }
    }
}
