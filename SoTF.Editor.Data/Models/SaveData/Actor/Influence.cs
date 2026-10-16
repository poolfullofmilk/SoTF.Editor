using Newtonsoft.Json;
using SoTF.Editor.Data.Resources;

namespace SoTF.Editor.Data.Models.SaveData.Actor;

public class Influence
{
    public float Anger { get; set; }

    public float Fear { get; set; }

    public float Sentiment { get; set; }

    public required string TypeId { get; init; }

    [JsonIgnore]
    public static IEnumerable<string> AllTypes => [Type.Player, Type.Cannibal, Type.Creepy];

    [JsonIgnore]
    public string PrintableTypeId => AppText.Get(TextKeys.Influence(TypeId));

    public static Influence AsFillerWithDefaults(string typeId)
    {
        return new Influence
        {
            Anger = 0,
            Fear = 0,
            Sentiment = 0,
            TypeId = typeId,
        };
    }

    public static class Type
    {
        public const string Player = "Player";
        public const string Cannibal = "Cannibal";
        public const string Creepy = "Creepy";
    }
}
