using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SoTF.Editor.Data.Models.SaveData.Storage.Module;
using SoTF.Editor.Data.Models.Storage;
using SoTF.Editor.Data.Resources;

namespace SoTF.Editor.Data.Models.SaveData.Storage;

public class StorageSaveDataModuleConverter : JsonConverter<IStorageModule>
{
    public override bool CanWrite => false;

    public override void WriteJson(
        JsonWriter writer,
        IStorageModule? value,
        JsonSerializer serializer
    )
    {
        throw new NotImplementedException();
    }

    public override IStorageModule? ReadJson(
        JsonReader reader,
        Type objectType,
        IStorageModule? existingValue,
        bool hasExistingValue,
        JsonSerializer serializer
    )
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return null;
        }

        // Throws When The Current Token Is Not An Object
        var obj = JObject.Load(reader);
        if (obj["ModuleId"]?.Value<int>() is not { } moduleId)
        {
            return new GenericModule(-1, obj);
        }

        IStorageModule instance = moduleId switch
        {
            1 => BuildSourceActorStorageModule(obj),
            3 => BuildFoodSpoilModule(moduleId, obj),
            _ => BuildGenericModule(moduleId, obj),
        };

        using var sr = obj.CreateReader();

        serializer.Populate(sr, instance);

        return instance;
    }

    private static SourceActorStorageModule BuildSourceActorStorageModule(JObject obj)
    {
        return new SourceActorStorageModule(
            obj["SourceActorType"]?.Value<int>() ?? SourceActorStorageModule.DefaultActorType
        );
    }

    private static GenericModule BuildGenericModule(int moduleId, JObject moduleToken)
    {
        return new GenericModule(moduleId, moduleToken);
    }

    private static FoodSpoilStorageModule BuildFoodSpoilModule(int moduleId, JObject obj)
    {
        return new FoodSpoilStorageModule(
            moduleId,
            obj["CurrentState"]?.Value<int>()
                ?? throw new InvalidDataException($"Found No Current State For Module {moduleId}"),
            obj["TimeRemainingInState"]?.Value<long>() ?? 0,
            obj["PauseDecay"]?.Value<bool>() ?? false
        );
    }
}
