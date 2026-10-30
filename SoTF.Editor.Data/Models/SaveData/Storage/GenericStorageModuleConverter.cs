using Newtonsoft.Json;
using SoTF.Editor.Data.Models.SaveData.Storage.Module;

namespace SoTF.Editor.Data.Models.SaveData.Storage;

public sealed class GenericStorageModuleConverter : JsonConverter<GenericModule>
{
    public override bool CanRead => false;

    public override void WriteJson(
        JsonWriter writer,
        GenericModule? value,
        JsonSerializer serializer
    )
    {
        value?.ModuleToken.WriteTo(writer);
    }

    public override GenericModule ReadJson(
        JsonReader reader,
        Type objectType,
        GenericModule? existingValue,
        bool hasExistingValue,
        JsonSerializer serializer
    )
    {
        throw new NotImplementedException();
    }
}
