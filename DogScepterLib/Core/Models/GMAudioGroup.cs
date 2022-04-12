namespace DogScepterLib.Core.Models;

/// <summary>
/// A representation of a GameMaker audio group.
/// </summary>
public class GMAudioGroup : IGMSerializable
{
    /// <summary>
    /// The name of the audio group.
    /// </summary>
    public GMString Name;

    public void Serialize(GMDataWriter writer)
    {
        writer.WritePointerString(Name);
    }

    public void Deserialize(GMDataReader reader)
    {
        Name = reader.ReadStringPointerObject();
    }

    public override string ToString()
    {
        return $"Audio Group: \"{Name.Content}\"";
    }
}