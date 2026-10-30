namespace SoTF.Editor.Data.Models.Actors;

public sealed class ActorGrouping(string name)
{
    public string Name { get; } = name;
    public List<ActorCollection> ActorCollections { get; } = [];
}
