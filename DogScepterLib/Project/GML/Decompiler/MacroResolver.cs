using System.Globalization;
using static DogScepterLib.Project.GML.Decompiler.MacroResolverTypes;

namespace DogScepterLib.Project.GML.Decompiler
{
    public static class MacroResolver
    {
        public enum MacroType
        {
            None,
            Boolean,
            Object,
            Sprite,
            Room,
            Font,
            Sound,
            Path,

            Color,
            Keyboard,

            PathEndAction,
            Gamepad,
            OSType,
        }

        public static ASTNode ResolveAny(DecompileContext ctx, ASTNode node, ASTNode parent, ConditionalMacroType type)
        {
            // Check if this type has a condition that needs to be satisfied
            if (type.Condition != null)
            {
                if (type.Condition.Evaluate(ctx, node, parent))
                {
                    if (type.Condition.EvaluateOnce)
                        type = new(type); // Make a new type without the condition (it has been satisfied)
                }
                else
                {
                    // Check if there's any valid alternative type
                    if (type.Alternatives != null)
                    {
                        bool evaluated = false;
                        for (int i = 0; i < type.Alternatives.Length; i++)
                        {
                            var curr = type.Alternatives[i];
                            if (curr.Condition == null || curr.Condition.Evaluate(ctx, node, parent))
                            {
                                evaluated = true;
                                if (curr.Condition != null && curr.Condition.EvaluateOnce)
                                    type = new(curr); // Make a new type without the condition (it has been satisfied)
                                else
                                    type = curr;
                                break;
                            }
                        }

                        if (!evaluated)
                            return node;
                    }
                    else
                        return node;
                }
            }

            switch (node.Kind)
            {
                case ASTNode.StatementKind.Int16:
                case ASTNode.StatementKind.Int32:
                    return ResolveInt(ctx, node, type);
                case ASTNode.StatementKind.IfStatement:
                    if (node.Children.Count == 5)
                    {
                        node.Children[3] = ResolveAny(ctx, node.Children[3], node, type);
                        node.Children[4] = ResolveAny(ctx, node.Children[4], node, type);
                    }
                    break;
                case ASTNode.StatementKind.Binary:
                    node.Children[0] = ResolveAny(ctx, node.Children[0], node, type);
                    node.Children[1] = ResolveAny(ctx, node.Children[1], node, type);
                    break;
                case ASTNode.StatementKind.Function:
                    // Check for functions that have the same type in all arguments generally (variable arguments usually)
                    string name = (node as ASTFunction).Function.Name.Content;
                    if (name == "@@NewGMLArray@@" || name == "choose")
                    {
                        for (int i = 0; i < node.Children.Count; i++)
                            node.Children[i] = ResolveAny(ctx, node.Children[i], node, type);
                    }
                    break;
            }

            return node;
        }

        public static ASTNode ResolveInt(DecompileContext ctx, ASTNode intNode, ConditionalMacroType type)
        {
            int value = (intNode.Kind == ASTNode.StatementKind.Int16) ? (intNode as ASTInt16).Value : (intNode as ASTInt32).Value;
            switch (type.Kind)
            {
                case MacroType.Boolean:
                    if (value == 0)
                        return new ASTBoolean(false);
                    else if (value == 1)
                        return new ASTBoolean(true);
                    break;
                case MacroType.Object:
                    if (intNode.Kind == ASTNode.StatementKind.Int16)
                        return ResolveObject(ctx, intNode as ASTInt16);
                    break;
                case MacroType.Sprite:
                    if (value >= 0 && value < ctx.Project.Sprites.Count)
                        return new ASTAsset(ctx.Project.Sprites[value].Name);
                    else if (value == -4)
                        return new ASTAsset("noone");
                    break;
                case MacroType.Room:
                    if (value >= 0 && value < ctx.Project.Rooms.Count)
                        return new ASTAsset(ctx.Project.Rooms[value].Name);
                    else if (value == -4)
                        return new ASTAsset("noone");
                    break;
                case MacroType.Font:
                    if (value >= 0 && value < ctx.Project.Fonts.Count)
                        return new ASTAsset(ctx.Project.Fonts[value].Name);
                    else if (value == -4)
                        return new ASTAsset("noone");
                    break;
                case MacroType.Sound:
                    if (value >= 0 && value < ctx.Project.Sounds.Count)
                        return new ASTAsset(ctx.Project.Sounds[value].Name);
                    else if (value == -4)
                        return new ASTAsset("noone");
                    break;
                case MacroType.Path:
                    if (value >= 0 && value < ctx.Project.Paths.Count)
                        return new ASTAsset(ctx.Project.Paths[value].Name);
                    else if (value == -4)
                        return new ASTAsset("noone");
                    break;
                case MacroType.Color:
                    if (value >= 0)
                    {
                        if (ctx.Cache.Types.ColorMacros.TryGetValue(value, out string color))
                            return new ASTAsset(color);
                        return new ASTAsset((ctx.Data.VersionInfo.IsNumberAtLeast(2) ? "0x" : "$") + value.ToString("X6", CultureInfo.InvariantCulture));
                    }
                    break;
                case MacroType.Keyboard:
                    if (value >= 0)
                    {
                        if (ctx.Cache.Types.KeyboardMacros.TryGetValue(value, out string keyboard))
                            return new ASTAsset(keyboard);
                        if (value >= '0' && value <= 'Z')
                            return new ASTAsset("ord(\"" + (char)value + "\")");
                    }
                    break;
                case MacroType.PathEndAction:
                    {
                        if (ctx.Cache.Types.PathEndActionMacros.TryGetValue(value, out string macro))
                            return new ASTAsset(macro);
                    }
                    break;
                case MacroType.Gamepad:
                    {
                        if (ctx.Cache.Types.GamepadMacros.TryGetValue(value, out string macro))
                            return new ASTAsset(macro);
                        else if (value == -4)
                            return new ASTAsset("noone");
                    }
                    break;
                case MacroType.OSType:
                    {
                        if (ctx.Cache.Types.OSTypeMacros.TryGetValue(value, out string macro))
                            return new ASTAsset(macro);
                    }
                    break;
            }
            return intNode;
        }

        public static void ResolveFunction(DecompileContext ctx, ASTFunction func)
        {
            // Handle code-entry-specific types
            if (ctx.CodeMacroTypes.HasValue)
            {
                if (ctx.CodeMacroTypes.Value.FunctionArgs != null &&
                    ctx.CodeMacroTypes.Value.FunctionArgs.TryGetValue(func.Function.Name.Content, out MacroType[] types))
                {
                    for (int i = 0; i < func.Children.Count && i < types.Length; i++)
                        func.Children[i] = ResolveAny(ctx, func.Children[i], func, new(types[i]));
                    return;
                }

                if (ctx.CodeMacroTypes.Value.FunctionArgsCond != null &&
                         ctx.CodeMacroTypes.Value.FunctionArgsCond.TryGetValue(func.Function.Name.Content, out var cond))
                {
                    for (int i = 0; i < func.Children.Count && i < cond.Length; i++)
                        func.Children[i] = ResolveAny(ctx, func.Children[i], func, cond[i]);
                    return;
                }
            }

            // Handle global types
            {
                if (ctx.Cache.Types.FunctionArgs.TryGetValue(func.Function.Name.Content, out MacroType[] types))
                {
                    for (int i = 0; i < func.Children.Count && i < types.Length; i++)
                        func.Children[i] = ResolveAny(ctx, func.Children[i], func, new(types[i]));
                    return;
                }

                if (ctx.Cache.Types.FunctionArgsCond.TryGetValue(func.Function.Name.Content, out var cond))
                {
                    for (int i = 0; i < func.Children.Count && i < cond.Length; i++)
                        func.Children[i] = ResolveAny(ctx, func.Children[i], func, cond[i]);
                    return;
                }
            }
        }

        public static void ResolveAssign(DecompileContext ctx, ASTAssign assign)
        {
            ConditionalMacroType left = GetExpressionType(ctx, assign.Children[0]);
            if (left.Kind != MacroType.None)
                assign.Children[1] = ResolveAny(ctx, assign.Children[1], assign, left);
        }

        public static void ResolveBinary(DecompileContext ctx, ASTBinary bin)
        {
            ConditionalMacroType left = GetExpressionType(ctx, bin.Children[0]);
            ConditionalMacroType right = GetExpressionType(ctx, bin.Children[1]);
            if (left.Kind != MacroType.None && right.Kind == MacroType.None)
                bin.Children[1] = ResolveAny(ctx, bin.Children[1], bin, left);
            else if (left.Kind == MacroType.None && right.Kind != MacroType.None)
                bin.Children[0] = ResolveAny(ctx, bin.Children[0], bin, right);
        }

        public static void ResolveSwitch(DecompileContext ctx, ASTSwitchStatement sw)
        {
            ConditionalMacroType expr = GetExpressionType(ctx, sw.Children[0]);
            if (expr.Kind != MacroType.None)
            {
                for (int i = 1; i < sw.Children.Count; i++)
                {
                    var curr = sw.Children[i];
                    if (curr.Kind == ASTNode.StatementKind.SwitchCase)
                        curr.Children[0] = ResolveAny(ctx, curr.Children[0], sw, expr);
                }
            }
        }

        public static void ResolveReturn(DecompileContext ctx, ASTReturn ret)
        {
            // Check for script return types
            MacroType type = MacroType.None;

            // ... code-entry-specific types
            if (ctx.CodeMacroTypes.HasValue)
                ctx.CodeMacroTypes.Value.FunctionReturns?.TryGetValue(ctx.CodeName, out type);

            // ... global types
            if (type == MacroType.None)
                ctx.Cache.Types.FunctionReturns.TryGetValue(ctx.CodeName, out type);

            // Then, actually apply the type if found
            if (type != MacroType.None)
                ret.Children[0] = ResolveAny(ctx, ret.Children[0], ret, new(type));
        }

        // Checks a node for a known/registered type (such as variables/function returns)
        public static ConditionalMacroType GetExpressionType(DecompileContext ctx, ASTNode node)
        {
            switch (node.Kind)
            {
                case ASTNode.StatementKind.Variable:
                    {
                        ASTVariable variable = node as ASTVariable;

                        // Handle code-entry-specific types
                        if (ctx.CodeMacroTypes.HasValue)
                        {
                            if (ctx.CodeMacroTypes.Value.VariableTypes != null &&
                                ctx.CodeMacroTypes.Value.VariableTypes.TryGetValue(variable.Variable.Name.Content, out MacroType type))
                                return new(type);
                            if (ctx.CodeMacroTypes.Value.VariableTypesCond != null &&
                                ctx.CodeMacroTypes.Value.VariableTypesCond.TryGetValue(variable.Variable.Name.Content, out var cond))
                                return cond;
                        }

                        // Handle global types
                        {
                            if (ctx.Cache.Types.VariableTypes.TryGetValue(variable.Variable.Name.Content, out MacroType type))
                                return new(type);
                            if (ctx.Cache.Types.VariableTypesCond.TryGetValue(variable.Variable.Name.Content, out var cond))
                                return cond;
                        }

                        if (variable.Variable.VariableID == -6)
                        {
                            // This is a builtin variable; check for builtin list
                            if (ctx.Cache.Types.VariableTypesBuiltin.TryGetValue(variable.Variable.Name.Content, out MacroType type))
                                return new(type);
                        }
                    }
                    break;
                case ASTNode.StatementKind.Function:
                    {
                        ASTFunction func = node as ASTFunction;

                        // Handle code-entry-specific types
                        if (ctx.CodeMacroTypes.HasValue)
                        {
                            if (ctx.CodeMacroTypes.Value.FunctionReturns != null &&
                                ctx.CodeMacroTypes.Value.FunctionReturns.TryGetValue(func.Function.Name.Content, out MacroType type))
                                return new(type);
                        }

                        // Handle global types
                        {
                            if (ctx.Cache.Types.FunctionReturns.TryGetValue(func.Function.Name.Content, out MacroType type))
                                return new(type);
                        }
                    }
                    break;
            }

            return new(MacroType.None);
        }

        public static ASTNode ResolveObject(DecompileContext ctx, ASTInt16 int16)
        {
            if (int16.Value < 0)
            {
                return int16.Value switch
                {
                    -1 => new ASTAsset("self"),
                    -2 => new ASTAsset("other"),
                    -3 => new ASTAsset("all"),
                    -4 => new ASTAsset("noone"),
                    _ => int16,
                };
            }

            if (int16.Value < ctx.Project.Objects.Count)
                return new ASTAsset(ctx.Project.Objects[int16.Value].Name);

            return int16;
        }
    }
}
