namespace DogScepterLib.Core.Models;

/// <summary>
/// A representation of an embedded audio file used in a GameMaker data file.
/// </summary>
public class GMAudio : IGMSerializable
{
    /// <summary>
    /// The binary audio data.
    /// </summary>
    public BufferRegion Data;

    public void Serialize(GMDataWriter writer)
    {
        writer.Write(Data.Length);
        writer.Write(Data);
    }

    public void Deserialize(GMDataReader reader)
    {
        int length = reader.ReadInt32();
        Data = reader.ReadBytes(length);
    }
}