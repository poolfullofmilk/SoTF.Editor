using SoTF.Editor.Data.Resources;

namespace SoTF.Editor.Data.Models.SaveData.Storage.Module;

public sealed class FoodSpoilStorageModuleWrapper(
    FoodSpoilStorageModule foodSpoilStorageModule,
    int itemId,
    int variantId
)
{
    public FoodSpoilStorageModule FoodSpoilStorageModule { get; } = foodSpoilStorageModule;
    public string Name => GameNames.Get(TextKeys.ItemVariant(itemId, variantId));
}
