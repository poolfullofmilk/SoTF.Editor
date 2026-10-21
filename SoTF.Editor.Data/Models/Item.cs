using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models.SaveData.Storage.Module;
using SoTF.Editor.Data.Models.Storage;
using SoTF.Editor.Data.Resources;

namespace SoTF.Editor.Data.Models;

public class Item : ICloneable
{
    public int Id { get; init; }
    public string Name => GameNames.Get(TextKeys.Item(Id));
    public string Type { get; init; } = "";
    public FoodSpoilModuleDefinition? FoodSpoilModuleDefinition { get; init; }
    public SourceActorModuleDefinition? SourceActorModuleDefinition { get; init; }
    public bool IsInventoryItem { get; init; } = true;
    public bool IsEquippableArmor { get; init; } = false;
    public bool IsWearableCloth { get; init; } = false;
    public bool IsPlatable { get; init; } = false;
    public DefaultMinMax? Durability { get; init; }
    public StorageMax? StorageMax { get; set; }
    public HashSet<int>? ModHashes { get; init; }
    public string? Image { get; init; }
    public string? ImageUrl => Image == null ? null : $"images/items/{Image}";

    public string NormalizedLowercaseName
    {
        get { return field ??= TranslationHelper.Normalize(Name).ToLower(); }
    }

    private string NormalizedLowercaseType
    {
        get { return field ??= TranslationHelper.Normalize(Type).ToLower(); }
    }

    public object Clone()
    {
        return MemberwiseClone();
    }

    public override bool Equals(object? obj)
    {
        return obj is Item other && Id == other.Id;
    }

    public override int GetHashCode()
    {
        return Id;
    }

    public bool Matches(string normalizedLowercaseFullText)
    {
        return NormalizedLowercaseName.Contains(normalizedLowercaseFullText)
            || NormalizedLowercaseType.Contains(normalizedLowercaseFullText);
    }
}

public record ItemModule(int ModuleId);

public record FoodSpoilModuleDefinition(int DefaultVariant, List<int> Variants) : ItemModule(3)
{
    public FoodSpoilStorageModule BuildNewModuleWithDefaults()
    {
        return new FoodSpoilStorageModule(ModuleId, DefaultVariant);
    }
}

public record SourceActorModuleDefinition(int DefaultSourceActorType) : ItemModule(1)
{
    public SourceActorStorageModule BuildNewModuleWithDefaults()
    {
        return new SourceActorStorageModule(DefaultSourceActorType);
    }
}
