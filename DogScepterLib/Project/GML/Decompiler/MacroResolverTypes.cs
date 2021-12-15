using DogScepterLib.Core.Chunks;
using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static DogScepterLib.Project.GML.Decompiler.MacroResolver;

namespace DogScepterLib.Project.GML.Decompiler
{
    public class MacroResolverTypes
    {

        public class ConditionalMacroType
        {
            public MacroType Kind { get; set; }
            public Condition Condition { get; set; }
            public ConditionalMacroType[] Alternatives { get; set; }

            public ConditionalMacroType()
            {
            }

            public ConditionalMacroType(MacroType kind)
            {
                Kind = kind;
            }

            public ConditionalMacroType(ConditionalMacroType otherWithCondition)
            {
                Kind = otherWithCondition.Kind;
            }
        }

        public interface Condition
        {
            public enum ConditionType
            {
                Any,
                All,
                FunctionArg,
                Nonzero,
                NonNode,
            }

            public ConditionType Kind { get; set; }
            public bool EvaluateOnce { get; set; }

            public bool Evaluate(DecompileContext ctx, ASTNode node, ASTNode parent);
        }

        // Evaluates to true if at least one sub-condition evaluates to true. Evaluates once by default, but can be changed.
        public class ConditionAny : Condition
        {
            public Condition.ConditionType Kind { get; set; } = Condition.ConditionType.Any;
            public bool EvaluateOnce { get; set; } = true;

            public Condition[] Conditions { get; set; }

            public bool Evaluate(DecompileContext ctx, ASTNode node, ASTNode parent)
            {
                foreach (var cond in Conditions)
                    if (cond.Evaluate(ctx, node, parent))
                        return true;
                return false;
            }
        }
        
        // Evaluates to true if every sub-condition evaluates to true. Evaluates once by default, but can be changed.
        public class ConditionAll : Condition
        {
            public Condition.ConditionType Kind { get; set; } = Condition.ConditionType.All;
            public bool EvaluateOnce { get; set; } = true;

            public Condition[] Conditions { get; set; }

            public bool Evaluate(DecompileContext ctx, ASTNode node, ASTNode parent)
            {
                foreach (var cond in Conditions)
                    if (!cond.Evaluate(ctx, node, parent))
                        return false;
                return true;
            }
        }

        // Evaluates to true if this node is the child of a function, which has a specific node argument. Evaluates once.
        public class ConditionFunctionArg : Condition
        {
            public Condition.ConditionType Kind { get; set; } = Condition.ConditionType.FunctionArg;
            public bool EvaluateOnce { get; set; } = true;

            public int Index { get; set; }
            public ASTNode.StatementKind NodeKind { get; set; }
            public string NodeValue { get; set; }

            public bool Evaluate(DecompileContext ctx, ASTNode node, ASTNode parent)
            {
                if (parent.Kind != ASTNode.StatementKind.Function)
                    return false;
                ASTFunction func = parent as ASTFunction;
                if (Index >= func.Children.Count)
                    return false;
                ASTNode arg = func.Children[Index];
                if (arg.Kind != NodeKind)
                    return false;
                if (arg.ToString() != NodeValue)
                    return false;
                return true;
            }
        }

        // Evaluates to true if the node is not an int16 zero. Evaluates multiple times.
        public class ConditionNonzero : Condition
        {
            public Condition.ConditionType Kind { get; set; } = Condition.ConditionType.Nonzero;
            public bool EvaluateOnce { get; set; } = false;

            public bool Evaluate(DecompileContext ctx, ASTNode node, ASTNode parent)
            {
                if (node.Kind == ASTNode.StatementKind.Int16)
                    if ((node as ASTInt16).Value == 0)
                        return false;
                // todo? other types like int32/64?
                return true;
            }
        }

        // Evaluates to true if the node is not a given node. Evaluates multiple times.
        public class ConditionNonNode : Condition
        {
            public Condition.ConditionType Kind { get; set; } = Condition.ConditionType.NonNode;
            public bool EvaluateOnce { get; set; } = false;

            public ASTNode.StatementKind NodeKind { get; set; }
            public string NodeValue { get; set; }

            public bool Evaluate(DecompileContext ctx, ASTNode node, ASTNode parent)
            {
                if (node.Kind != NodeKind)
                    return true;
                if (node.ToString() != NodeValue)
                    return true;
                return false;
            }
        }

        public class ConditionConverter : JsonConverter<Condition>
        {
            public override bool CanConvert(Type typeToConvert) =>
                typeToConvert == typeof(Condition);

            public override Condition Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                Utf8JsonReader readerClone = reader;
                if (readerClone.TokenType != JsonTokenType.StartObject)
                    throw new JsonException();
                readerClone.Read();
                if (readerClone.TokenType != JsonTokenType.PropertyName)
                    throw new JsonException();
                if (readerClone.GetString() != "Kind")
                    throw new JsonException();
                readerClone.Read();
                if (readerClone.TokenType != JsonTokenType.String)
                    throw new JsonException();
                if (Enum.TryParse(readerClone.GetString(), out Condition.ConditionType kind))
                {
                    return kind switch
                    {
                        Condition.ConditionType.Any => JsonSerializer.Deserialize<ConditionAny>(ref reader, options),
                        Condition.ConditionType.All => JsonSerializer.Deserialize<ConditionAll>(ref reader, options),
                        Condition.ConditionType.FunctionArg => JsonSerializer.Deserialize<ConditionFunctionArg>(ref reader, options),
                        Condition.ConditionType.Nonzero => JsonSerializer.Deserialize<ConditionNonzero>(ref reader, options),
                        Condition.ConditionType.NonNode => JsonSerializer.Deserialize<ConditionNonNode>(ref reader, options),
                        _ => throw new JsonException()
                    };
                }

                throw new JsonException();
            }

            public override void Write(Utf8JsonWriter writer, Condition value, JsonSerializerOptions options)
            {
                JsonSerializer.Serialize(writer, value, value.GetType(), options);
            }
        }

        public ProjectFile Project;
        public Dictionary<string, MacroType[]> FunctionArgs;
        public Dictionary<string, ConditionalMacroType[]> FunctionArgsCond = new();
        public Dictionary<string, MacroType> FunctionReturns;
        public Dictionary<string, MacroType> VariableTypes = new();
        public Dictionary<string, MacroType> VariableTypesBuiltin;
        public Dictionary<string, ConditionalMacroType> VariableTypesCond = new();
        public Dictionary<string, MacroResolverTypeJson> CodeEntries;

        public Dictionary<int, string> ColorMacros;
        public Dictionary<int, string> KeyboardMacros;
        public Dictionary<int, string> PathEndActionMacros;

        public MacroResolverTypes(ProjectFile pf)
        {
            Project = pf;

            FunctionArgs = new Dictionary<string, MacroType[]>()
            {
                { "instance_create", new[] { MacroType.None, MacroType.None, MacroType.Object } },
                { "instance_create_depth", new[] { MacroType.None, MacroType.None, MacroType.None, MacroType.Object } },
                { "instance_exists", new[] { MacroType.Object } },
                { "instance_create_layer", new[] { MacroType.None, MacroType.None, MacroType.None, MacroType.Object } },
                { "instance_activate_object", new[] { MacroType.Object } },
                { "instance_change", new[] { MacroType.Object, MacroType.Boolean } },
                { "instance_copy", new[] { MacroType.Boolean } },
                { "instance_destroy", new[] { MacroType.Object, MacroType.Boolean } },
                { "instance_find", new[] { MacroType.Object, MacroType.None } },
                { "instance_furthest", new[] { MacroType.None, MacroType.None, MacroType.Object } },
                { "instance_nearest", new[] { MacroType.None, MacroType.None, MacroType.Object } },
                { "instance_number", new[] { MacroType.Object } },
                { "instance_place", new[] { MacroType.None, MacroType.None, MacroType.Object } },
                { "instance_position", new[] { MacroType.None, MacroType.None, MacroType.Object } },
                { "instance_deactivate_all", new[] { MacroType.Boolean } },
                { "instance_deactivate_object", new[] { MacroType.Object } },
                { "instance_activate_region", new[] { MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.Boolean } },
                { "instance_deactivate_region", new[] { MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.Boolean , MacroType.Boolean } },

                { "place_meeting", new[] { MacroType.None, MacroType.None, MacroType.Object } },
                { "position_meeting", new[] { MacroType.None, MacroType.None, MacroType.Object } },
                { "position_change", new[] { MacroType.None, MacroType.None, MacroType.Object, MacroType.None } },
                { "collision_point", new[] { MacroType.None, MacroType.None, MacroType.Object, MacroType.None, MacroType.None } },
                { "collision_line", new[] { MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.Object, MacroType.Boolean, MacroType.Boolean } },
                { "collision_rectangle", new[] { MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.Object, MacroType.None, MacroType.None } },
                { "collision_circle", new[] { MacroType.None, MacroType.None, MacroType.None, MacroType.Object, MacroType.None, MacroType.None } },
                { "collision_ellipse", new[] { MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.Object, MacroType.None, MacroType.None } },
                { "distance_to_object", new[] { MacroType.Object } },

                { "application_surface_enable", new[] { MacroType.Boolean } },
                { "application_surface_draw_enable", new[] { MacroType.Boolean } },

                { "draw_set_color", new[] { MacroType.Color } },
                { "draw_set_colour", new[] { MacroType.Color } },
                { "draw_sprite", new[] { MacroType.Sprite } },
                { "draw_sprite_ext", new[] { MacroType.Sprite, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.Color, MacroType.None } },
                { "draw_sprite_general", new[] { MacroType.Sprite, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.Color, MacroType.Color, MacroType.Color, MacroType.Color, MacroType.None } },
                { "draw_sprite_part", new[] { MacroType.Sprite, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None } },
                { "draw_sprite_part_ext", new[] { MacroType.Sprite, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.Color, MacroType.None } },
                { "draw_sprite_stretched", new[] { MacroType.Sprite, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None } },
                { "draw_sprite_stretched_ext", new[] { MacroType.Sprite, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.Color, MacroType.None } },
                { "draw_sprite_pos", new[] { MacroType.Sprite, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None } },
                { "draw_sprite_tiled", new[] { MacroType.Sprite, MacroType.None, MacroType.None, MacroType.None } },
                { "draw_sprite_tiled_ext", new[] { MacroType.Sprite, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.Color, MacroType.None } },

                { "draw_set_font", new[] { MacroType.Font } },
                { "draw_text_color", new[] { MacroType.None, MacroType.None, MacroType.None, MacroType.Color, MacroType.Color, MacroType.Color, MacroType.Color, MacroType.None } },
                { "draw_text_ext_color", new[] { MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.Color, MacroType.Color, MacroType.Color, MacroType.Color, MacroType.None } },
                { "draw_text_transformed_color", new[] { MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.Color, MacroType.Color, MacroType.Color, MacroType.Color, MacroType.None } },
                { "draw_text_transformed_ext_color", new[] { MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.Color, MacroType.Color, MacroType.Color, MacroType.Color, MacroType.None } },
                { "draw_text_colour", new[] { MacroType.None, MacroType.None, MacroType.None, MacroType.Color, MacroType.Color, MacroType.Color, MacroType.Color, MacroType.None } },
                { "draw_text_ext_colour", new[] { MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.Color, MacroType.Color, MacroType.Color, MacroType.Color, MacroType.None } },
                { "draw_text_transformed_colour", new[] { MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.Color, MacroType.Color, MacroType.Color, MacroType.Color, MacroType.None } },
                { "draw_text_transformed_ext_colour", new[] { MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.Color, MacroType.Color, MacroType.Color, MacroType.Color, MacroType.None } },

                { "draw_rectangle", new[] { MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.Boolean } },

                { "room_goto", new[] { MacroType.Room } },

                { "merge_color", new[] { MacroType.Color, MacroType.Color, MacroType.None } },

                { "keyboard_check", new[] { MacroType.Keyboard } },
                { "keyboard_check_pressed", new[] { MacroType.Keyboard } },
                { "keyboard_check_released", new[] { MacroType.Keyboard } },
                { "keyboard_check_direct", new[] { MacroType.Keyboard } },
                { "keyboard_clear", new[] { MacroType.Keyboard } },
                { "keyboard_key_press", new[] { MacroType.Keyboard } },
                { "keyboard_key_release", new[] { MacroType.Keyboard } },
                { "keyboard_set_map", new[] { MacroType.Keyboard, MacroType.Keyboard } },
                { "keyboard_get_map", new[] { MacroType.Keyboard } },
                { "keyboard_unset_map", new[] { MacroType.Keyboard } },
                { "keyboard_set_numlock", new[] { MacroType.Boolean } },

                { "sprite_get_name", new[] { MacroType.Sprite } },
                { "sprite_get_number", new[] { MacroType.Sprite } },
                { "sprite_get_width", new[] { MacroType.Sprite } },
                { "sprite_get_height", new[] { MacroType.Sprite } },
                { "sprite_get_xoffset", new[] { MacroType.Sprite } },
                { "sprite_get_yoffset", new[] { MacroType.Sprite } },
                { "sprite_get_bbox_bottom", new[] { MacroType.Sprite } },
                { "sprite_get_bbox_left", new[] { MacroType.Sprite } },
                { "sprite_get_bbox_right", new[] { MacroType.Sprite } },
                { "sprite_get_bbox_top", new[] { MacroType.Sprite } },
                { "sprite_get_tpe", new[] { MacroType.Sprite, MacroType.None } },
                { "sprite_get_texture", new[] { MacroType.Sprite, MacroType.None } },
                { "sprite_get_uvs", new[] { MacroType.Sprite, MacroType.None } },

                { "sprite_exists", new[] { MacroType.Sprite } },
                { "sprite_add", new[] { MacroType.None, MacroType.None, MacroType.Boolean, MacroType.Boolean, MacroType.None, MacroType.None } },
                { "sprite_replace", new[] { MacroType.Sprite, MacroType.None, MacroType.None, MacroType.Boolean, MacroType.Boolean, MacroType.None, MacroType.None } },
                { "sprite_duplicate", new[] { MacroType.Sprite } },
                { "sprite_assign", new[] { MacroType.Sprite, MacroType.Sprite } },
                { "sprite_merge", new[] { MacroType.Sprite, MacroType.Sprite } },
                { "sprite_create_from_surface", new[] { MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.Boolean, MacroType.Boolean, MacroType.None, MacroType.None } },
                { "sprite_add_from_surface", new[] { MacroType.Sprite, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.Boolean, MacroType.Boolean } },
                { "sprite_collision_mask", new[] { MacroType.Sprite, MacroType.Boolean, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None } },
                { "sprite_set_offset", new[] { MacroType.Sprite, MacroType.None, MacroType.None } },
                { "sprite_delete", new[] { MacroType.Sprite } },
                { "sprite_set_alpha_from_sprite", new[] { MacroType.Sprite, MacroType.Sprite } },
                { "sprite_set_cache_size", new[] { MacroType.Sprite, MacroType.None } },
                { "sprite_set_cache_size_ext", new[] { MacroType.Sprite, MacroType.None, MacroType.None } },
                { "sprite_save", new[] { MacroType.Sprite, MacroType.None, MacroType.None } },
                { "sprite_save_strip", new[] { MacroType.Sprite, MacroType.None } },
                { "sprite_flush", new[] { MacroType.Sprite } },
                { "sprite_flush_multi", new[] { MacroType.Sprite } },
                { "sprite_prefetch", new[] { MacroType.Sprite } },
                { "sprite_prefetch_multi", new[] { MacroType.Sprite } },

                { "audio_exists", new[] { MacroType.Sound } },
                { "audio_get_name", new[] { MacroType.Sound } },
                { "audio_get_type", new[] { MacroType.Sound } },
                { "audio_play_sound", new[] { MacroType.Sound, MacroType.None, MacroType.Boolean } },
                { "audio_play_sound_at", new[] { MacroType.Sound, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.None, MacroType.Boolean, MacroType.None } },
                { "audio_pause_sound", new[] { MacroType.Sound } },
                { "audio_pause_all", Array.Empty<MacroType>() },
                { "audio_resume_sound", new[] { MacroType.Sound } },
                { "audio_resume_all", Array.Empty<MacroType>() },
                { "audio_stop_sound", new[] { MacroType.Sound } },
                { "audio_stop_all", Array.Empty<MacroType>() },
                { "audio_is_playing", new[] { MacroType.Sound } },
                { "audio_is_paused", new[] { MacroType.Sound } },
                { "audio_create_streaam", new[] { MacroType.None } },
                { "audio_destroy_streaam", new[] { MacroType.None } },

                { "audio_sound_set_track_position", new[] { MacroType.Sound, MacroType.None } },
                { "audio_sound_get_track_position", new[] { MacroType.Sound } },
                { "audio_sound_length", new[] { MacroType.Sound } },
                { "audio_sound_pitch", new[] { MacroType.Sound, MacroType.None } },
                { "audio_sound_get_pitch", new[] { MacroType.Sound } },
                { "audio_falloff_set_model", new[] { MacroType.None } },
                { "audio_sound_gain", new[] { MacroType.Sound, MacroType.None, MacroType.None } },
                { "audio_sound_get_gain", new[] { MacroType.Sound } },
                { "audio_master_gain", new[] { MacroType.None } },
                { "audio_play_sound_on", new[] { MacroType.None, MacroType.Sound, MacroType.Boolean, MacroType.None } },
                { "audio_play_in_sync_group", new[] { MacroType.None, MacroType.Sound } },

                { "path_start", new[] { MacroType.Path, MacroType.None, MacroType.Macro_PathEndAction, MacroType.Boolean } },
                { "path_end", Array.Empty<MacroType>() },

                { "path_exists", new[] { MacroType.Path } },
                { "path_get_closed", new[] { MacroType.Path } },
                { "path_get_kind", new[] { MacroType.Path } },
                { "path_get_length", new[] { MacroType.Path } },
                { "path_get_name", new[] { MacroType.Path } },
                { "path_get_number", new[] { MacroType.Path } },
                { "path_get_point_speed", new[] { MacroType.Path, MacroType.None } },
                { "path_get_point_x", new[] { MacroType.Path, MacroType.None } },
                { "path_get_point_y", new[] { MacroType.Path, MacroType.None } },
                { "path_get_precision", new[] { MacroType.Path, MacroType.None } },
                { "path_get_speed", new[] { MacroType.Path, MacroType.None } },
                { "path_get_x", new[] { MacroType.Path, MacroType.None } },
                { "path_get_y", new[] { MacroType.Path, MacroType.None} },

                { "path_add", Array.Empty<MacroType>() },
                { "path_add_point", new[] { MacroType.Path, MacroType.None, MacroType.None, MacroType.None } },
                { "path_change_point", new[] { MacroType.Path, MacroType.None, MacroType.None, MacroType.None, MacroType.None } },
                { "path_insert_point", new[] { MacroType.Path, MacroType.None, MacroType.None, MacroType.None, MacroType.None } },
                { "path_delete_point", new[] { MacroType.Path, MacroType.None } },
                { "path_clear_points", new[] { MacroType.Path } },
                { "path_append", new[] { MacroType.Path, MacroType.Path } },
                { "path_assign", new[] { MacroType.Path, MacroType.Path } },
                { "path_delete", new[] { MacroType.Path } },
                { "path_duplicate", new[] { MacroType.Path } },
                { "path_flip", new[] { MacroType.Path } },
                { "path_mirror", new[] { MacroType.Path } },
                { "path_reverse", new[] { MacroType.Path } },
                { "path_rotate", new[] { MacroType.Path, MacroType.None } },
                { "path_scale", new[] { MacroType.Path, MacroType.None, MacroType.None } },
                { "path_set_closed", new[] { MacroType.Path, MacroType.None } },
                { "path_set_kind", new[] { MacroType.Path, MacroType.None } },
                { "path_set_precision", new[] { MacroType.Path, MacroType.None } },
                { "path_shift", new[] { MacroType.Path, MacroType.None, MacroType.None } },
            };

            FunctionReturns = new Dictionary<string, MacroType>()
            {
                { "instance_exists", MacroType.Boolean },
                { "draw_get_color", MacroType.Color },
                { "draw_get_font", MacroType.Font },
                { "keyboard_check", MacroType.Boolean },
                { "keyboard_check_pressed", MacroType.Boolean },
                { "keyboard_check_released", MacroType.Boolean },
                { "keyboard_check_direct", MacroType.Boolean },
                { "sprite_exists", MacroType.Boolean },
            };

            VariableTypesBuiltin = new Dictionary<string, MacroType>()
            {
                { "sprite_index", MacroType.Sprite },
                { "mask_index", MacroType.Sprite },
                { "object_index", MacroType.Object },
                { "room", MacroType.Room },
                { "image_blend", MacroType.Color },
                { "path_index", MacroType.Path },
            };

            ColorMacros = new Dictionary<int, string>()
            {
                { 16776960, "c_aqua" },
                { 0, "c_black" },
                { 16711680, "c_blue" },
                { 4210752, "c_dkgray" },
                { 16711935, "c_fuchsia" },
                { 8421504, "c_gray" },
                { 32768, "c_green" },
                { 65280, "c_lime" },
                { 12632256, "c_ltgray" },
                { 128, "c_maroon" },
                { 8388608, "c_navy" },
                { 32896, "c_olive" },
                { 8388736, "c_purple" },
                { 255, "c_red" },
                // { 12632256, "c_silver" },
                { 8421376, "c_teal" },
                { 16777215, "c_white" },
                { 65535, "c_yellow" },
                { 4235519, "c_orange" }
            };

            KeyboardMacros = new Dictionary<int, string>()
            {
                { 0, "vk_nokey" },
                { 1, "vk_anykey" },
                { 8, "vk_backspace" },
                { 9, "vk_tab" },
                { 13, "vk_enter" },
                { 16, "vk_shift" },
                { 17, "vk_control" },
                { 18, "vk_alt" },
                { 19, "vk_pause" },
                { 27, "vk_escape" },
                { 32, "vk_space" },
                { 33, "vk_pageup" },
                { 34, "vk_pagedown" },
                { 35, "vk_end" },
                { 36, "vk_home" },
                { 37, "vk_left" },
                { 38, "vk_up" },
                { 39, "vk_right" },
                { 40, "vk_down" },
                { 44, "vk_printscreen" },
                { 45, "vk_insert" },
                { 46, "vk_delete" },
                { 96, "vk_numpad0" },
                { 97, "vk_numpad1" },
                { 98, "vk_numpad2" },
                { 99, "vk_numpad3" },
                { 100, "vk_numpad4" },
                { 101, "vk_numpad5" },
                { 102, "vk_numpad6" },
                { 103, "vk_numpad7" },
                { 104, "vk_numpad8" },
                { 105, "vk_numpad9" },
                { 106, "vk_multiply" },
                { 107, "vk_add" },
                { 109, "vk_subtract" },
                { 110, "vk_decimal" },
                { 111, "vk_divide" },
                { 112, "vk_f1" },
                { 113, "vk_f2" },
                { 114, "vk_f3" },
                { 115, "vk_f4" },
                { 116, "vk_f5" },
                { 117, "vk_f6" },
                { 118, "vk_f7" },
                { 119, "vk_f8" },
                { 120, "vk_f9" },
                { 121, "vk_f10" },
                { 122, "vk_f11" },
                { 123, "vk_f12" },
                { 160, "vk_lshift" },
                { 161, "vk_rshift" },
                { 162, "vk_lcontrol" },
                { 163, "vk_rcontrol" },
                { 164, "vk_lalt" },
                { 165, "vk_ralt" }
            };

            PathEndActionMacros = new Dictionary<int, string>()
            {
                { 0, "path_action_stop" },
                { 1, "path_action_restart" },
                { 2, "path_action_continue" },
                { 3, "path_action_reverse" }
            };
        }

        public struct MacroResolverTypeJson
        {
            public Dictionary<string, MacroType[]> FunctionArgs { get; set; }
            public Dictionary<string, ConditionalMacroType[]> FunctionArgsCond { get; set; }
            public Dictionary<string, MacroType> FunctionReturns { get; set; }
            public Dictionary<string, MacroType> VariableTypes { get; set; }
            public Dictionary<string, ConditionalMacroType> VariableTypesCond { get; set; }
            public Dictionary<string, MacroResolverTypeJson> CodeEntries { get; set; }
        }

        public bool AddFromConfigFile(string name)
        {
            return AddFromFile(Path.Combine(GameConfigs.MacroTypesDirectory, name + ".json"));
        }

        public bool AddFromFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                AddFromFile(File.ReadAllBytes(filePath));
                return true;
            }
            return false;
        }

        public void AddFromFile(byte[] data)
        {
            var options = new JsonSerializerOptions(ProjectFile.JsonOptions);
            options.Converters.Add(new ConditionConverter());
            var json = JsonSerializer.Deserialize<MacroResolverTypeJson>(data, options);
            if (json.FunctionArgs != null)
            {
                foreach (var kvp in json.FunctionArgs)
                    FunctionArgs[kvp.Key] = kvp.Value;
            }
            if (json.FunctionArgsCond != null)
            {
                foreach (var kvp in json.FunctionArgsCond)
                    FunctionArgsCond[kvp.Key] = kvp.Value;
            }
            if (json.FunctionReturns != null)
            {
                foreach (var kvp in json.FunctionReturns)
                    FunctionReturns[kvp.Key] = kvp.Value;
            }
            if (json.VariableTypes != null)
            {
                foreach (var kvp in json.VariableTypes)
                    VariableTypes[kvp.Key] = kvp.Value;
            }
            if (json.VariableTypesCond != null)
            {
                foreach (var kvp in json.VariableTypesCond)
                    VariableTypesCond[kvp.Key] = kvp.Value;
            }
            if (json.CodeEntries != null)
            {
                CodeEntries = new Dictionary<string, MacroResolverTypeJson>();
                GMChunkOBJT objt = Project.DataHandle.GetChunk<GMChunkOBJT>();
                GMChunkCODE code = Project.DataHandle.GetChunk<GMChunkCODE>();
                foreach (var kvp in json.CodeEntries)
                {
                    if (kvp.Key.StartsWith("object: "))
                    {
                        GMObject obj = objt.List[Project.Objects.FindIndex(kvp.Key[8..])];
                        foreach (var eventType in obj.Events)
                        {
                            foreach (var subEvent in eventType)
                            {
                                foreach (var action in subEvent.Actions)
                                {
                                    if (action.CodeID >= 0)
                                        CodeEntries[code.List[action.CodeID].Name.Content] = kvp.Value;
                                }
                            }
                        }
                    }
                    else
                        CodeEntries[kvp.Key] = kvp.Value;
                }
            }
        }
    }
}
