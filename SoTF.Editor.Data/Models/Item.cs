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
    public string? ThumbnailUrl =>
        Image == null ? null : "images/items/" + Image.ExtendFilenameWith("_tn");

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

    private bool Equals(Item other)
    {
        return Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        return obj.GetType() == GetType() && Equals((Item)obj);
    }

    public override int GetHashCode()
    {
        return Id;
    }

    public bool HasFoodSpoilModuleDefinition()
    {
        return FoodSpoilModuleDefinition != null;
    }

    public bool HasSourceActorModuleDefinition()
    {
        return SourceActorModuleDefinition != null;
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
