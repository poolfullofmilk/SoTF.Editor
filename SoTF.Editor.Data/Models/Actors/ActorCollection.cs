namespace SoTF.Editor.Data.Models.Actors;

public class ActorCollection
{
    public ActorCollection(string name, IEnumerable<Actor> actors)
    {
        Actors.AddRange(actors);
        Name = name;
    }

    public string Name { get; }
    public List<Actor> Actors { get; } = [];

    // One Type Throughout Names Every Row The Same
    public ActorType? SharedType =>
        Actors.Select(actor => actor.TypeId).Distinct().Take(2).Count() == 1
            ? Actors[0].ActorType
            : null;
}
