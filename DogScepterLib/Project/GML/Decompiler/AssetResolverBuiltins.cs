using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static DogScepterLib.Project.GML.Decompiler.AssetResolver;

namespace DogScepterLib.Project.GML.Decompiler
{
    public class AssetResolverBuiltins
    {
        public Dictionary<string, AssetType[]> FunctionArgs;
        public Dictionary<string, AssetType> FunctionReturns;
        public Dictionary<string, AssetType> VariableTypes;
        public Dictionary<int, string> ColorMacros;
        public Dictionary<int, string> KeyboardMacros;

        public AssetResolverBuiltins()
        {
            FunctionArgs = new Dictionary<string, AssetType[]>()
            {
                { "instance_create", new[] { AssetType.None, AssetType.None, AssetType.Object } },
                { "instance_create_depth", new[] { AssetType.None, AssetType.None, AssetType.None, AssetType.Object } },
                { "instance_exists", new[] { AssetType.Object } },
                { "instance_create_layer", new[] { AssetType.None, AssetType.None, AssetType.None, AssetType.Object } },
                { "instance_activate_object", new[] { AssetType.Object } },
                { "instance_change", new[] { AssetType.Object, AssetType.Boolean } },
                { "instance_copy", new[] { AssetType.Boolean } },
                { "instance_destroy", new[] { AssetType.Object, AssetType.Boolean } },
                { "instance_find", new[] { AssetType.Object, AssetType.None } },
                { "instance_furthest", new[] { AssetType.None, AssetType.None, AssetType.Object } },
                { "instance_nearest", new[] { AssetType.None, AssetType.None, AssetType.Object } },
                { "instance_number", new[] { AssetType.Object } },
                { "instance_place", new[] { AssetType.None, AssetType.None, AssetType.Object } },
                { "instance_position", new[] { AssetType.None, AssetType.None, AssetType.Object } },
                { "instance_deactivate_all", new[] { AssetType.Boolean } },
                { "instance_deactivate_object", new[] { AssetType.Object } },
                { "instance_activate_region", new[] { AssetType.None, AssetType.None, AssetType.None, AssetType.None, AssetType.Boolean } },
                { "instance_deactivate_region", new[] { AssetType.None, AssetType.None, AssetType.None, AssetType.None, AssetType.Boolean , AssetType.Boolean } },

                { "application_surface_enable", new[] { AssetType.Boolean } },
                { "application_surface_draw_enable", new[] { AssetType.Boolean } },

                { "draw_set_color", new[] { AssetType.Color } },
                { "draw_sprite", new[] { AssetType.Sprite } },
                { "draw_sprite_ext", new[] { AssetType.Sprite, AssetType.None, AssetType.None, AssetType.None, AssetType.None, AssetType.None, AssetType.None, AssetType.Color, AssetType.None } },
                { "draw_sprite_general", new[] { AssetType.Sprite, AssetType.None, AssetType.None, AssetType.None, AssetType.None, AssetType.None, AssetType.None, AssetType.None, AssetType.None, AssetType.None, AssetType.None, AssetType.Color, AssetType.Color, AssetType.Color, AssetType.Color, AssetType.None } },
                { "draw_sprite_part", new[] { AssetType.Sprite, AssetType.None, AssetType.None, AssetType.None, AssetType.None, AssetType.None, AssetType.None, AssetType.None } },
                { "draw_sprite_part_ext", new[] { AssetType.Sprite, AssetType.None, AssetType.None, AssetType.None, AssetType.None, AssetType.None, AssetType.None, AssetType.None, AssetType.None, AssetType.None, AssetType.Color, AssetType.None } },
                { "draw_sprite_stretched", new[] { AssetType.Sprite, AssetType.None, AssetType.None, AssetType.None, AssetType.None, AssetType.None } },
                { "draw_sprite_stretched_ext", new[] { AssetType.Sprite, AssetType.None, AssetType.None, AssetType.None, AssetType.None, AssetType.None, AssetType.Color, AssetType.None } },
                { "draw_sprite_pos", new[] { AssetType.Sprite, AssetType.None, AssetType.None, AssetType.None, AssetType.None, AssetType.None, AssetType.None, AssetType.None, AssetType.None, AssetType.None, AssetType.None } },
                { "draw_sprite_tiled", new[] { AssetType.Sprite, AssetType.None, AssetType.None, AssetType.None } },
                { "draw_sprite_tiled_ext", new[] { AssetType.Sprite, AssetType.None, AssetType.None, AssetType.None, AssetType.None, AssetType.None, AssetType.Color, AssetType.None } },

                { "draw_set_font", new[] { AssetType.Font } },

                { "room_goto", new[] { AssetType.Room } },

                { "merge_color", new[] { AssetType.Color, AssetType.Color, AssetType.None } },

                { "keyboard_check", new[] { AssetType.Keyboard } },
                { "keyboard_check_pressed", new[] { AssetType.Keyboard } },
                { "keyboard_check_released", new[] { AssetType.Keyboard } },
                { "keyboard_check_direct", new[] { AssetType.Keyboard } },
                { "keyboard_clear", new[] { AssetType.Keyboard } },
                { "keyboard_key_press", new[] { AssetType.Keyboard } },
                { "keyboard_key_release", new[] { AssetType.Keyboard } },
                { "keyboard_set_map", new[] { AssetType.Keyboard, AssetType.Keyboard } },
                { "keyboard_get_map", new[] { AssetType.Keyboard } },
                { "keyboard_unset_map", new[] { AssetType.Keyboard } },
                { "keyboard_set_numlock", new[] { AssetType.Boolean } },

                { "sprite_get_name", new[] { AssetType.Sprite } },
                { "sprite_get_number", new[] { AssetType.Sprite } },
                { "sprite_get_width", new[] { AssetType.Sprite } },
                { "sprite_get_height", new[] { AssetType.Sprite } },
                { "sprite_get_xoffset", new[] { AssetType.Sprite } },
                { "sprite_get_yoffset", new[] { AssetType.Sprite } },
                { "sprite_get_bbox_bottom", new[] { AssetType.Sprite } },
                { "sprite_get_bbox_left", new[] { AssetType.Sprite } },
                { "sprite_get_bbox_right", new[] { AssetType.Sprite } },
                { "sprite_get_bbox_top", new[] { AssetType.Sprite } },
                { "sprite_get_tpe", new[] { AssetType.Sprite, AssetType.None } },
                { "sprite_get_texture", new[] { AssetType.Sprite, AssetType.None } },
                { "sprite_get_uvs", new[] { AssetType.Sprite, AssetType.None } },

                { "sprite_exists", new[] { AssetType.Sprite } },
                { "sprite_add", new[] { AssetType.None, AssetType.None, AssetType.Boolean, AssetType.Boolean, AssetType.None, AssetType.None } },
                { "sprite_replace", new[] { AssetType.Sprite, AssetType.None, AssetType.None, AssetType.Boolean, AssetType.Boolean, AssetType.None, AssetType.None } },
                { "sprite_duplicate", new[] { AssetType.Sprite } },
                { "sprite_assign", new[] { AssetType.Sprite, AssetType.Sprite } },
                { "sprite_merge", new[] { AssetType.Sprite, AssetType.Sprite } },
                { "sprite_create_from_surface", new[] { AssetType.None, AssetType.None, AssetType.None, AssetType.None, AssetType.None, AssetType.Boolean, AssetType.Boolean, AssetType.None, AssetType.None } },
                { "sprite_add_from_surface", new[] { AssetType.Sprite, AssetType.None, AssetType.None, AssetType.None, AssetType.None, AssetType.None, AssetType.Boolean, AssetType.Boolean } },
                { "sprite_collision_mask", new[] { AssetType.Sprite, AssetType.Boolean, AssetType.None, AssetType.None, AssetType.None, AssetType.None, AssetType.None, AssetType.None, AssetType.None } },
                { "sprite_set_offset", new[] { AssetType.Sprite, AssetType.None, AssetType.None } },
                { "sprite_delete", new[] { AssetType.Sprite } },
                { "sprite_set_alpha_from_sprite", new[] { AssetType.Sprite, AssetType.Sprite } },
                { "sprite_set_cache_size", new[] { AssetType.Sprite, AssetType.None } },
                { "sprite_set_cache_size_ext", new[] { AssetType.Sprite, AssetType.None, AssetType.None } },
                { "sprite_save", new[] { AssetType.Sprite, AssetType.None, AssetType.None } },
                { "sprite_save_strip", new[] { AssetType.Sprite, AssetType.None } },
                { "sprite_flush", new[] { AssetType.Sprite } },
                { "sprite_flush_multi", new[] { AssetType.Sprite } },
                { "sprite_prefetch", new[] { AssetType.Sprite } },
                { "sprite_prefetch_multi", new[] { AssetType.Sprite } },

                { "audio_exists", new[] { AssetType.Sound } },
                { "audio_get_name", new[] { AssetType.Sound } },
                { "audio_get_type", new[] { AssetType.Sound } },
                { "audio_play_sound", new[] { AssetType.Sound, AssetType.None, AssetType.Boolean } },
                { "audio_play_sound_at", new[] { AssetType.Sound, AssetType.None, AssetType.None, AssetType.None, AssetType.None, AssetType.None, AssetType.None, AssetType.Boolean, AssetType.None } },
                { "audio_pause_sound", new[] { AssetType.Sound } },
                { "audio_pause_all", Array.Empty<AssetType>() },
                { "audio_resume_sound", new[] { AssetType.Sound } },
                { "audio_resume_all", Array.Empty<AssetType>() },
                { "audio_stop_sound", new[] { AssetType.Sound } },
                { "audio_stop_all", Array.Empty<AssetType>() },
                { "audio_is_playing", new[] { AssetType.Sound } },
                { "audio_is_paused", new[] { AssetType.Sound } },
                { "audio_create_streaam", new[] { AssetType.None } },
                { "audio_destroy_streaam", new[] { AssetType.None } },

                { "audio_sound_set_track_position", new[] { AssetType.Sound, AssetType.None } },
                { "audio_sound_get_track_position", new[] { AssetType.Sound } },
                { "audio_sound_length", new[] { AssetType.Sound } },
                { "audio_sound_pitch", new[] { AssetType.Sound, AssetType.None } },
                { "audio_sound_get_pitch", new[] { AssetType.Sound } },
                { "audio_falloff_set_model", new[] { AssetType.None } },
                { "audio_sound_gain", new[] { AssetType.Sound, AssetType.None, AssetType.None } },
                { "audio_sound_get_gain", new[] { AssetType.Sound } },
                { "audio_master_gain", new[] { AssetType.None } },
                { "audio_play_sound_on", new[] { AssetType.None, AssetType.Sound, AssetType.Boolean, AssetType.None } },
                { "audio_play_in_sync_group", new[] { AssetType.None, AssetType.Sound } },
            };

            FunctionReturns = new Dictionary<string, AssetType>()
            {
                { "instance_exists", AssetType.Boolean },
                { "draw_get_color", AssetType.Color },
                { "draw_get_font", AssetType.Font },
                { "keyboard_check", AssetType.Boolean },
                { "keyboard_check_pressed", AssetType.Boolean },
                { "keyboard_check_released", AssetType.Boolean },
                { "keyboard_check_direct", AssetType.Boolean },
                { "sprite_exists", AssetType.Boolean },
            }; 
            
            VariableTypes = new Dictionary<string, AssetType>()
            {
                { "sprite_index", AssetType.Sprite },
                { "mask_index", AssetType.Sprite },
                { "room", AssetType.Room },
                { "image_blend", AssetType.Color },
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
        }
    }
}
