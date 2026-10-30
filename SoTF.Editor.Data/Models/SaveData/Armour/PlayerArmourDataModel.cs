using Newtonsoft.Json;

namespace SoTF.Editor.Data.Models.SaveData.Armour;

public sealed record PlayerArmourDataModel : SotfBaseModel
{
    public required DataModel Data { get; init; }

    public record DataModel
    {
        [JsonConverter(typeof(StringTypeConverter))]
        public required PlayerArmourSystemModel PlayerArmourSystem { get; init; }
    }
}
