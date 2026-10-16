namespace SoTF.Editor.Data.Models.Actors;

public class ActorCollection
{
    public ActorCollection(string name, IEnumerable<Actor> actors)
    {
        Actors.AddRange(actors);
        Name = name;
    }

    public string Name => $"{field} ({Actors.Count})";
    public List<Actor> Actors { get; } = [];
}
