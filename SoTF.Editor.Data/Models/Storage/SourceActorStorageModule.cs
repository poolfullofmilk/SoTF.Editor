using SoTF.Editor.Data.Models.SaveData.Storage.Module;

namespace SoTF.Editor.Data.Models.Storage;

public record SourceActorStorageModule(int SourceActorType) : BaseStorageModule(1)
{
    public static int DefaultActorType => 13;
}
