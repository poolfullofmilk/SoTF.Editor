using Newtonsoft.Json;
using SoTF.Editor.Data.Resources;

namespace SoTF.Editor.Data.Models.SaveData.Actor;

public sealed class Influence
{
    public float Anger { get; set; }

    public float Fear { get; set; }

    public float Sentiment { get; set; }

    public required string TypeId { get; init; }

    public static IEnumerable<string> AllTypes => [Type.Player, Type.Cannibal, Type.Creepy];

    [JsonIgnore]
    public string PrintableTypeId => AppText.Get(TextKeys.Influence(TypeId));

    public static class Type
    {
        public const string Player = "Player";
        public const string Cannibal = "Cannibal";
        public const string Creepy = "Creepy";
    }
}
