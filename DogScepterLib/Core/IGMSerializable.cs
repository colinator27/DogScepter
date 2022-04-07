using DogScepterLib.Core.Models;

namespace DogScepterLib.Core;

/// <summary>
/// A GameMaker resource that can be read and written.
/// </summary>
public interface IGMSerializable
{
    /// <summary>
    /// Serializes this GameMaker resource into a specified <see cref="GMDataWriter"/>.
    /// </summary>
    /// <param name="writer">The <see cref="GMDataWriter"/> from where to serialize to.</param>
    void Serialize(GMDataWriter writer);

    /// <summary>
    /// Deserializes a GameMaker resource from a specified <see cref="GMDataReader"/>.
    /// </summary>
    /// <param name="reader">The <see cref="GMDataReader"/> from where to deserialize from.</param>
    void Deserialize(GMDataReader reader);
}

/// <summary>
/// A GameMaker resource, which contains a name, that can be read and written.
/// </summary>
public interface IGMNamedSerializable : IGMSerializable
{
    /// <summary>
    /// The name of a GameMaker resource.
    /// </summary>
    public GMString Name { get; set; }
}