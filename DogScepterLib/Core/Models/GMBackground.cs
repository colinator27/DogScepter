using System.Collections.Generic;

namespace DogScepterLib.Core.Models;

/// <summary>
/// Contains a tileset since GameMaker Studio 2, contains a background otherwise.
/// </summary>
/// <remarks>While the GMS1.X IDE does show tile information, those are only for the IDE. Internally, they're all still backgrounds.</remarks>
public class GMBackground : IGMNamedSerializable
{
    /// <summary>
    /// The name of the tileset/background.
    /// </summary>
    public GMString Name { get; set; }

    /// <summary>
    /// Whether the background is transparent. Only affects backgrounds, and thus only GameMaker Studio 1.X.
    /// </summary>
    public bool Transparent;

    /// <summary>
    /// Whether to smooth the edges of a background. Only has an affect if <see cref="Transparent"/> is true.
    /// Only affects backgrounds, and thus only GameMaker Studio 1.X.
    /// </summary>
    public bool Smooth;

    /// <summary>
    /// TODO: find more info about this. I couldn't find this setting in neither gm8, nor gms1, nor in the docs.
    /// </summary>
    public bool Preload;

    /// <summary>
    /// Which texture group this tileset/background is assigned to.
    /// </summary>
    public GMTextureItem TextureItem;

    // GMS2 tiles

    /// <summary>
    /// A currently unknown tile value. Seems to always be 2.
    /// </summary>
    public uint TileUnknown1 = 2;

    /// <summary>
    /// The width of a tile in this tileset. Only used in GameMaker Studio 2.
    /// </summary>
    public uint TileWidth;

    /// <summary>
    /// The height of a tile in this tileset. Only used in GameMaker Studio 2.
    /// </summary>
    public uint TileHeight;

    // Borders generated around each tile
    /// <summary>
    /// The amount of extra empty pixels left and right next to a tile. Only used in GameMaker Studio 2.
    /// </summary>
    public uint TileOutputBorderX;

    /// <summary>
    /// The amount of extra empty pixels above and below a tile. Only used in GameMaker Studio 2.
    /// </summary>
    public uint TileOutputBorderY;

    /// <summary>
    /// The amount of columns this tileset has. Only used in GameMaker Studio 2.
    /// </summary>
    public uint TileColumns;

    /// <summary>
    /// A currently unknown tile value. Seems to always be 0.
    /// </summary>
    public uint TileUnknown2 = 0;

    /// <summary>
    /// The time for each frame in microseconds. Only used in GameMaker Studio 2.
    /// </summary>
    public long TileFrameLength;

    /// <summary>
    /// Contains entries per tile per frame. Only used in GameMaker Studio 2.
    /// </summary>
    public List<List<uint>> Tiles;

    public void Serialize(GMDataWriter writer)
    {
        writer.WritePointerString(Name);
        writer.WriteWideBoolean(Transparent);
        writer.WriteWideBoolean(Smooth);
        writer.WriteWideBoolean(Preload);
        writer.WritePointer(TextureItem);

        // If pre gms2, we serialized everything and we can stop.
        if (writer.VersionInfo.Major < 2) return;

        writer.Write(TileUnknown1);
        writer.Write(TileWidth);
        writer.Write(TileHeight);
        writer.Write(TileOutputBorderX);
        writer.Write(TileOutputBorderY);
        writer.Write(TileColumns);
        writer.Write((uint)Tiles[0].Count);
        writer.Write((uint)Tiles.Count);
        writer.Write(TileUnknown2);
        writer.Write(TileFrameLength);

        for (int i = 0; i < Tiles.Count; i++)
        {
            if (i != 0 && Tiles[i].Count != Tiles[i-1].Count)
                writer.Warnings.Add(new GMWarning("Amount of frames is different across tiles", GMWarning.WarningLevel.Severe));
            foreach (uint item in Tiles[i])
            {
                writer.Write(item);
            }
        }
    }

    public void Deserialize(GMDataReader reader)
    {
        Name = reader.ReadStringPointerObject();
        Transparent = reader.ReadWideBoolean();
        Smooth = reader.ReadWideBoolean();
        Preload = reader.ReadWideBoolean();
        TextureItem = reader.ReadPointerObject<GMTextureItem>();

        // If pre gms2, we deserialized everything and we can stop.
        if (reader.VersionInfo.Major < 2) return;

        TileUnknown1 = reader.ReadUInt32();
        if (TileUnknown1 != 2)
            reader.Warnings.Add(new GMWarning("Expected 2 in BGND"));
        TileWidth = reader.ReadUInt32();
        TileHeight = reader.ReadUInt32();
        TileOutputBorderX = reader.ReadUInt32();
        TileOutputBorderY = reader.ReadUInt32();
        TileColumns = reader.ReadUInt32();
        uint tileFrameCount = reader.ReadUInt32();
        uint tileCount = reader.ReadUInt32();
        TileUnknown2 = reader.ReadUInt32();
        if (TileUnknown2 != 0)
            reader.Warnings.Add(new GMWarning("Expected 0 in BGND"));
        TileFrameLength = reader.ReadInt64();

        Tiles = new List<List<uint>>((int)tileCount);
        for (int i = 0; i < tileCount; i++)
        {
            List<uint> tileFrames = new List<uint>((int)tileFrameCount);
            Tiles.Add(tileFrames);
            for (int j = 0; j < tileFrameCount; j++)
            {
                tileFrames.Add(reader.ReadUInt32());
            }
        }

    }

    public override string ToString()
    {
        return $"Background: \"{Name.Content}\"";
    }
}