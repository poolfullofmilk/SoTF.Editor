using Newtonsoft.Json;

namespace SoTF.Editor.Data.Models.SaveData.Storage.Module;

public record FoodSpoilStorageModule(
    int ModuleId,
    [property: JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)] int CurrentState,
    [property: JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        long TimeRemainingInState = 86400,
    [property: JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        bool PauseDecay = true
) : BaseStorageModule(ModuleId)
{
    public override bool IsEqualTo(IStorageModule? other)
    {
        return base.IsEqualTo(other)
            && other is FoodSpoilStorageModule module
            && module.CurrentState == CurrentState;
    }
}
