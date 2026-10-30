using Newtonsoft.Json;
using SoTF.Editor.Data.Models.SaveData.Actor;
using SoTF.Editor.Data.Resources;

namespace SoTF.Editor.Data.Models.Actors;

public sealed class Actor
{
    public ActorType? ActorType { get; set; }
    public int? FamilyId { get; set; }
    public List<Influence>? Influences { get; set; }
    public KillStat? KillStats { get; set; }
    public float? LastVisitTime { get; set; }
    public Position? Position { get; set; }
    public int? SpawnerId { get; set; }
    public Spawner? Spawner { get; set; }
    public int State { get; set; }

    [JsonIgnore]
    public string StatePrintable => AppText.Get(TextKeys.ActorState(State), $"??? ({State})");

    public Dictionary<string, float>? Stats { get; set; }
    public int TypeId { get; set; }
    public int UniqueId { get; set; }
    public int OutfitId { get; set; }
    public int? GraphMask { get; set; }

    [JsonIgnore]
    public string ImageUrl =>
        ActorType?.Image is { } image
            ? $"images/actors/{image}"
            : ActorType?.IconPath ?? "images/icons/question-mark.svg";

    // The Map Search Still Finds An Actor By Its Id
    public string PrintableTitle => $"{ActorType?.Name ?? "???"} · {UniqueId}";
}
