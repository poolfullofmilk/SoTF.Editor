using Newtonsoft.Json;
using SoTF.Editor.Data.Models.SaveData.Storage.Module;

namespace SoTF.Editor.Data.Models.Storage;

public record SourceActorStorageModule(
    [property: JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        int SourceActorType,
    [property: JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)] int ActorSeed = 0
) : BaseStorageModule(1);
