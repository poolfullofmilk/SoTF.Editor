using System.Globalization;
using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.Map;

namespace SoTF.Editor.Data.Resources;

public static class TextKeys
{
    public static string Item(int id) => $"Item{id}";

    public static string ItemVariant(int itemId, int variantId) =>
        $"Item{itemId}Variant{variantId}";

    public static string ActorType(int id) => $"ActorType{id}";

    public static string Structure(int id) => $"Structure{id}";

    public static string Outfit(int typeId, int id) => $"Outfit{typeId}Id{id}";

    public static string WorldItem(WorldItemType type) => $"WorldItem{type}";

    public static string ItemType(string type) => "ItemType" + Pascal(type);

    public static string StructureCategory(string category) =>
        "StructureCategory" + Pascal(category);

    public static string Classification(string classification) =>
        "Classification" + Pascal(classification);

    public static string Gender(string gender) => "Gender" + Pascal(gender);

    public static string Influence(string type) => "Influence" + type;

    public static string ActorState(int state) =>
        state < 0 ? "ActorStateNone" : $"ActorState{state}";

    public static string RequirementsFilter(MapFilter.RequirementsFilterType filter) =>
        $"RequirementsFilter{filter}";

    public static string SpawnPattern(SpawnPattern pattern) => $"SpawnPattern{pattern}";

    public static string TeleportTarget(TeleportTarget target) => $"TeleportTarget{target}";

    public static string PoiGroup(PoiGroupType type) => $"PoiGroup{type}";

    public static string StructureMode(ScrewStructureModificationMode mode) =>
        $"StructureMode{mode}";

    public static string Setting(string name) => "Setting" + Pascal(name.TrimStart('_'));

    // Game State And Weather Fields The Pages Show
    public static IReadOnlyList<string> SettingNames { get; } =
    [
        "GameName",
        "SaveTime",
        "GameType",
        "GameDays",
        "GameHours",
        "GameMinutes",
        "GameSeconds",
        "GameMilliseconds",
        "IsVirginiaDead",
        "IsRobbyDead",
        "CrashSite",
        "_isRaining",
        "_rainBlocked",
        "_cloudState",
        "_currentRainType",
        "_currentSeason",
    ];

    private static string Pascal(string text)
    {
        return text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
    }

    public static List<string> MissingKeys(GameData gameData)
    {
        // Every Runtime Key, Built The Way The App Builds It
        var gameKeys = gameData
            .Items.Select(item => Item(item.Key))
            .Concat(gameData.ActorTypes.Select(actorType => ActorType(actorType.Id)))
            .Concat(gameData.ScrewStructures.Select(structure => Structure(structure.Id)))
            .Concat(gameData.StorageDefinitions.Select(storage => Structure(storage.Id)))
            .Concat(gameData.AdvancedStorageDefinitions.Select(storage => Structure(storage.Id)))
            .Concat(
                gameData.Items.SelectMany(item =>
                    item.Value.FoodSpoilModuleDefinition?.Variants.Select(variant =>
                        ItemVariant(item.Key, variant)
                    )
                    ?? []
                )
            )
            .Concat(
                new[] { Constants.Actors.KelvinTypeId, Constants.Actors.VirginiaTypeId }.SelectMany(
                    typeId =>
                        gameData
                            .FollowerData.GetOutfits(typeId)
                            .Where(outfit => outfit.Id >= 0)
                            .Select(outfit => Outfit(typeId, outfit.Id))
                )
            )
            .Concat(
                Enum.GetValues<WorldItemType>()
                    .Where(type => type != WorldItemType.Unknown)
                    .Select(WorldItem)
            );

        var appKeys = gameData
            .Items.Select(item => item.Value.Type)
            .Where(type => type.Length > 0)
            .Select(ItemType)
            .Concat(
                gameData
                    .ScrewStructures.Select(structure => structure.Category)
                    .Where(category => category.Length > 0)
                    .Select(StructureCategory)
            )
            .Concat(
                gameData.ActorTypes.Select(actorType => Classification(actorType.Classification))
            )
            .Concat(
                gameData
                    .ActorTypes.Select(actorType => actorType.Gender)
                    .OfType<string>()
                    .Select(Gender)
            )
            .Concat(Models.SaveData.Actor.Influence.AllTypes.Select(Influence))
            .Concat(Enum.GetValues<MapFilter.RequirementsFilterType>().Select(RequirementsFilter))
            .Concat(Enum.GetValues<SpawnPattern>().Select(SpawnPattern))
            .Concat(Enum.GetValues<TeleportTarget>().Select(TeleportTarget))
            .Concat(Enum.GetValues<ScrewStructureModificationMode>().Select(StructureMode))
            .Concat(SettingNames.Select(Setting))
            .Concat(
                Enum.GetValues<PoiGroupType>()
                    .Where(type => type is >= PoiGroupType.Bunkers and <= PoiGroupType.Lakes)
                    .Select(PoiGroup)
            );

        var english = CultureInfo.InvariantCulture;
        var dutch = CultureInfo.GetCultureInfo("nl");
        return
        [
            .. gameKeys
                .Where(key => !GameNames.Has(key))
                .Concat(
                    appKeys.Where(key => !AppText.Has(key, english) || !AppText.Has(key, dutch))
                )
                .Distinct(),
        ];
    }
}
