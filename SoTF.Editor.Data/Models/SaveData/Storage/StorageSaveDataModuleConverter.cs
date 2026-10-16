using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SoTF.Editor.Data.Models.SaveData.Storage.Module;
using SoTF.Editor.Data.Models.Storage;

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
        var moduleToken = JObject.Load(reader);
        if (moduleToken["ModuleId"]?.Value<int>() is not { } moduleId)
        {
            return new GenericModule(-1, moduleToken);
        }

        // Missing Fields Are Zero, The Game Leaves Them Out
        IStorageModule instance = moduleId switch
        {
            1 => new SourceActorStorageModule(0),
            3 => new FoodSpoilStorageModule(moduleId, 0, 0, false),
            _ => new GenericModule(moduleId, moduleToken),
        };

        using var moduleReader = moduleToken.CreateReader();
        serializer.Populate(moduleReader, instance);

        return instance;
    }
}
