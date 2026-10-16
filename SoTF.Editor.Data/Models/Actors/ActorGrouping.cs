namespace SoTF.Editor.Data.Models.Actors;

public class ActorGrouping(string name)
{
    public string Name => $"{name} ({ActorCollections.Count})";
    public List<ActorCollection> ActorCollections { get; } = [];
}
