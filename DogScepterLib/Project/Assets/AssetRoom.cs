using DogScepterLib.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace DogScepterLib.Project.Assets
{
    public class AssetRoom : Asset
    {
        public string Caption { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Speed { get; set; }
        public bool Persistent { get; set; }
        public int BackgroundColor { get; set; }
        public bool DrawBackgroundColor { get; set; }
        public string CreationCode { get; set; }
        public bool EnableViews { get; set; }
        public bool ShowColor { get; set; }
        public bool ClearDisplayBuffer { get; set; }
        public List<Background> Backgrounds { get; set; }
        public List<View> Views { get; set; }
        public List<GameObject> GameObjects { get; set; }
        public List<Tile> Tiles { get; set; }
        public List<Layer> Layers { get; set; }
        public List<string> Sequences { get; set; }
        public PhysicsSettings Physics { get; set; }

        public new static Asset Load(string assetPath)
        {
            byte[] buff = File.ReadAllBytes(assetPath);
            var res = JsonSerializer.Deserialize<AssetRoom>(buff, ProjectFile.JsonOptions);
            ComputeHash(res, buff);
            return res;
        }

        protected override byte[] WriteInternal(ProjectFile pf, string assetPath, bool actuallyWrite)
        {
            var options = new JsonSerializerOptions(ProjectFile.JsonOptions);
            options.WriteIndented = pf.HackyComparisonMode;
            byte[] buff = JsonSerializer.SerializeToUtf8Bytes(this, options);
            if (actuallyWrite)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
                using (FileStream fs = new FileStream(assetPath, FileMode.Create))
                    fs.Write(buff, 0, buff.Length);
            }
            return buff;
        }

        public override void Delete(string assetPath)
        {
            if (File.Exists(assetPath))
                File.Delete(assetPath);

            string dir = Path.GetDirectoryName(assetPath);
            if (Directory.Exists(dir))
                Directory.Delete(dir);
        }

        public struct PhysicsSettings
        {
            public bool Enabled { get; set; }
            public int Top { get; set; }
            public int Left { get; set; }
            public int Right { get; set; }
            public int Bottom { get; set; }
            public float GravityX { get; set; }
            public float GravityY { get; set; }
            public float PixelsToMeters { get; set; }
        }

        public struct Background
        {
            public bool Enabled { get; set; }
            public bool Foreground { get; set; }
            public string Asset { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
            public int TileX { get; set; }
            public int TileY { get; set; }
            public int SpeedX { get; set; }
            public int SpeedY { get; set; }
            public bool Stretch { get; set; }
        }

        public struct View
        {
            public bool Enabled { get; set; }
            public int ViewX { get; set; }
            public int ViewY { get; set; }
            public int ViewWidth { get; set; }
            public int ViewHeight { get; set; }
            public int PortX { get; set; }
            public int PortY { get; set; }
            public int PortWidth { get; set; }
            public int PortHeight { get; set; }
            public int BorderX { get; set; }
            public int BorderY { get; set; }
            public int SpeedX { get; set; }
            public int SpeedY { get; set; }
            public string FollowObject { get; set; }
        }

        public struct GameObject
        {
            public int X { get; set; }
            public int Y { get; set; }
            public string Asset { get; set; }
            public int InstanceID { get; set; }
            public string CreationCode { get; set; }
            public float ScaleX { get; set; }
            public float ScaleY { get; set; }
            public int Color { get; set; }
            public float Angle { get; set; }
            
            // ~1.4.9999+
            public string PreCreateCode { get; set; }

            // 2.2.2.302+
            public float ImageSpeed { get; set; }
            public int ImageIndex { get; set; }
        }

        public struct Tile
        {
            public int X { get; set; }
            public int Y { get; set; }
            public string Asset { get; set; }
            public int SourceX { get; set; }
            public int SourceY { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public int Depth { get; set; }
            public int ID { get; set; }
            public float ScaleX { get; set; }
            public float ScaleY { get; set; }
            public int Color { get; set; }
        }

        public struct Layer
        {
            public string Name { get; set; }
            public int ID { get; set; }
            public int Depth { get; set; }
            public float OffsetX { get; set; }
            public float OffsetY { get; set; }
            public float HSpeed { get; set; }
            public float VSpeed { get; set; }
            public bool Visible { get; set; }
            public LayerBackground Background { get; set; }
            public GMRoom.Layer.LayerInstances Instances { get; set; }
            public LayerAssets Assets { get; set; }
            public LayerTiles Tiles { get; set; }
            public LayerEffect Effect { get; set; }
            public EffectsSettings EffectNew { get; set; }

            public class LayerBackground
            {
                public bool Visible { get; set; }
                public bool Foreground { get; set; }
                public string Sprite { get; set; }
                public bool TileHorz { get; set; }
                public bool TileVert { get; set; }
                public bool Stretch { get; set; }
                public int Color { get; set; }
                public float FirstFrame { get; set; }
                public float AnimationSpeed { get; set; }
                public GMSprite.AnimSpeedType AnimationSpeedType { get; set; }
            }

            public class LayerAssets
            {
                public List<Tile> LegacyTiles { get; set; }
                public List<AssetInstance> Sprites { get; set; }
                public List<AssetInstance> Sequences { get; set; }
                public List<AssetInstance> NineSlices { get; set; }

                public struct AssetInstance
                {
                    public string Name { get; set; }
                    public string Asset { get; set; }
                    public int X { get; set; }
                    public int Y { get; set; }
                    public float ScaleX { get; set; }
                    public float ScaleY { get; set; }
                    public int Color { get; set; }
                    public float AnimationSpeed { get; set; }
                    public GMSprite.AnimSpeedType AnimationSpeedType { get; set; }
                    public float FrameIndex { get; set; }
                    public float Rotation { get; set; }
                }
            }

            public class LayerTiles
            {
                public string Background { get; set; }
                public int TilesX { get; set; }
                public int TilesY { get; set; }
                public int[][] TileData { get; set; }
            }

            public class LayerEffect
            {
                public string EffectType { get; set; }
                public List<EffectProperty> Properties { get; set; }

                public struct EffectProperty
                {
                    public GMRoom.Layer.EffectProperty.PropertyType Kind { get; set; }
                    public string Name { get; set; }
                    public string Value { get; set; }
                }
            }

            public struct EffectsSettings
            {
                public bool Enabled { get; set; }
                public string Type { get; set; }
                public List<Layer.LayerEffect.EffectProperty> Properties { get; set; }
            }
        }
    }
}
