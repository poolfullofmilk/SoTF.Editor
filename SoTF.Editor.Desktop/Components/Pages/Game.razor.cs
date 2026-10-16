using Microsoft.AspNetCore.Components;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Desktop.Components.Shared.Common;
using SoTF.Editor.Services.Pages;
using static SoTF.Editor.Services.Pages.GameSetupPageViewModel;

namespace SoTF.Editor.Desktop.Components.Pages;

public partial class Game : EditorPageBase
{
    // Values The Game Writes, Labels From The Translations
    private static readonly SettingOption[] s_modes =
    [
        new("Normal", "Normal"),
        new(HardGameMode, "Hard"),
        new(HardSurvivalGameMode, "HardSurvival"),
        new(PeacefulGameMode, "Peaceful"),
        new(CreativeGameMode, "ModeCreative"),
        new(CustomGameMode, "ModeCustom"),
    ];

    private static readonly SettingOption[] s_offNormalHard =
    [
        new(SettingValueOff, "Off"),
        new(SettingValueNormal, "Normal"),
        new(SettingValueHard, "Hard"),
    ];

    private static readonly SettingOption[] s_lowNormalHigh =
    [
        new(SettingValueLOW, "Low"),
        new(SettingValueNORMAL, "Normal"),
        new(SettingValueHigh, "High"),
    ];

    private static readonly SettingOption[] s_pvpDamage =
    [
        new(SettingValueOff, "Off"),
        new(SettingValueReduced, "Reduced"),
        new(SettingValueNormal, "Normal"),
    ];

    private static readonly SettingOption[] s_seasons =
    [
        new("Spring", "Spring"),
        new("Summer", "Summer"),
        new("Autumn", "Autumn"),
        new("Winter", "Winter"),
    ];

    private static readonly SettingOption[] s_seasonLengths =
    [
        new("Short", "Short"),
        new("Default", "Default"),
        new("Long", "Long"),
        new("Realistic", "Realistic"),
    ];

    private static readonly SettingOption[] s_dayLengths =
    [
        new("Short", "Short"),
        new("Default", "Default"),
        new("Long", "Long"),
        new("Realistic", "Realistic"),
    ];

    private static readonly SettingOption[] s_precipitation =
    [
        new("Low", "Low"),
        new("Default", "Default"),
        new(SettingValueHigh, "High"),
    ];

    [Inject]
    public required GameSetupPageViewModel GameSetup { get; set; }

    [Inject]
    public required GameStatePageViewModel GameState { get; set; }

    [Inject]
    public required WeatherPageViewModel Weather { get; set; }

    private string? _namedIntFilter;

    private bool IsCustom => GameSetup.SelectedMode == CustomGameMode;

    private IEnumerable<GenericSetting> FilteredNamedIntDatas =>
        string.IsNullOrWhiteSpace(_namedIntFilter)
            ? GameState.NamedIntDatas
            : GameState.NamedIntDatas.Where(setting =>
                setting.Name.Contains(_namedIntFilter, StringComparison.OrdinalIgnoreCase)
            );

    private string LabelOf(IEnumerable<SettingOption> options, string? value)
    {
        return options.FirstOrDefault(option => option.Value == value) is { } option
            ? Localizer[option.LabelKey]
            : value ?? string.Empty;
    }

    private IEnumerable<SettingGroup> SetupGroups =>
        [
            new(
                "CustomSettings",
                [
                    new(
                        "ConsumableEffects",
                        () => GameSetup.SelectedConsumableEffects,
                        value => GameSetup.SelectedConsumableEffects = value,
                        s_offNormalHard
                    ),
                    new(
                        "PlayerStatsDamage",
                        () => GameSetup.SelectedPlayerStatsDamage,
                        value => GameSetup.SelectedPlayerStatsDamage = value,
                        s_offNormalHard
                    ),
                    new(
                        "AnimalSpawnRate",
                        () => GameSetup.SelectedAnimalSpawnRate,
                        value => GameSetup.SelectedAnimalSpawnRate = value,
                        s_lowNormalHigh
                    ),
                    new(
                        "ColdPenalties",
                        () => GameSetup.SelectedColdPenalties,
                        value => GameSetup.SelectedColdPenalties = value,
                        s_offNormalHard
                    ),
                    new(
                        "BuildingResistance",
                        () => GameSetup.SelectedBuildingResistance,
                        value => GameSetup.SelectedBuildingResistance = value,
                        s_lowNormalHigh
                    ),
                    new(
                        "StatRegenerationPenalties",
                        () => GameSetup.SelectedColdPenaltiesStatReduction,
                        value => GameSetup.SelectedColdPenaltiesStatReduction = value,
                        s_offNormalHard
                    ),
                ],
                [
                    new(
                        "InventoryPause",
                        () => GameSetup.SelectedInventoryPause,
                        value => GameSetup.SelectedInventoryPause = value
                    ),
                    new(
                        "ReducedFood",
                        () => GameSetup.SelectedReducedFoodInContainers,
                        value => GameSetup.SelectedReducedFoodInContainers = value
                    ),
                    new(
                        "ReducedAmmo",
                        () => GameSetup.SelectedReducedAmmoInContainers,
                        value => GameSetup.SelectedReducedAmmoInContainers = value
                    ),
                    new(
                        "SingleUseContainers",
                        () => GameSetup.SelectedSingleUseContainers,
                        value => GameSetup.SelectedSingleUseContainers = value
                    ),
                    new(
                        "PlayersTriggerTraps",
                        () => GameSetup.SelectedPlayersTriggerTraps,
                        value => GameSetup.SelectedPlayersTriggerTraps = value
                    ),
                    new(
                        "CreativeMode",
                        () => GameSetup.SelectedCreativeMode,
                        value => GameSetup.SelectedCreativeMode = value
                    ),
                    new(
                        "GodMode",
                        () => GameSetup.SelectedPlayersImmortalMode,
                        value => GameSetup.SelectedPlayersImmortalMode = value
                    ),
                    new(
                        "OneHitTrees",
                        () => GameSetup.SelectedOneHitToCutTrees,
                        value => GameSetup.SelectedOneHitToCutTrees = value
                    ),
                    new(
                        "InstantBuilding",
                        () => GameSetup.SelectedForcePlaceFullLoad,
                        value => GameSetup.SelectedForcePlaceFullLoad = value
                    ),
                    new(
                        "NoCuttingsSpawn",
                        () => GameSetup.SelectedNoCuttingsSpawn,
                        value => GameSetup.SelectedNoCuttingsSpawn = value
                    ),
                ]
            ),
            new(
                "Enemies",
                [
                    new(
                        "EnemyHealth",
                        () => GameSetup.SelectedEnemyHealth,
                        value => GameSetup.SelectedEnemyHealth = value,
                        s_lowNormalHigh
                    ),
                    new(
                        "EnemyDamage",
                        () => GameSetup.SelectedEnemyDamage,
                        value => GameSetup.SelectedEnemyDamage = value,
                        s_lowNormalHigh
                    ),
                    new(
                        "EnemyArmor",
                        () => GameSetup.SelectedEnemyArmour,
                        value => GameSetup.SelectedEnemyArmour = value,
                        s_lowNormalHigh
                    ),
                    new(
                        "EnemyAggression",
                        () => GameSetup.SelectedEnemyAggression,
                        value => GameSetup.SelectedEnemyAggression = value,
                        s_lowNormalHigh
                    ),
                    new(
                        "EnemySearchParties",
                        () => GameSetup.SelectedEnemySearchParties,
                        value => GameSetup.SelectedEnemySearchParties = value,
                        s_lowNormalHigh
                    ),
                ],
                [
                    new(
                        "SpawnEnemies",
                        () => GameSetup.SelectedEnemySpawn,
                        value => GameSetup.SelectedEnemySpawn = value
                    ),
                ]
            ),
            new(
                "Weather",
                [
                    new(
                        "StartingSeason",
                        () => GameSetup.SelectedStartingSeason,
                        value => GameSetup.SelectedStartingSeason = value,
                        s_seasons
                    ),
                    new(
                        "SeasonLength",
                        () => GameSetup.SelectedSeasonLength,
                        value => GameSetup.SelectedSeasonLength = value,
                        s_seasonLengths
                    ),
                    new(
                        "DayLength",
                        () => GameSetup.SelectedDayLength,
                        value => GameSetup.SelectedDayLength = value,
                        s_dayLengths
                    ),
                    new(
                        "PrecipitationFrequency",
                        () => GameSetup.SelectedPrecipitationFrequency,
                        value => GameSetup.SelectedPrecipitationFrequency = value,
                        s_precipitation
                    ),
                ],
                []
            ),
            new(
                "Multiplayer",
                [
                    new(
                        "PvpDamage",
                        () => GameSetup.SelectedPvpDamage,
                        value => GameSetup.SelectedPvpDamage = value,
                        s_pvpDamage
                    ),
                ],
                []
            ),
        ];

    private sealed record SettingOption(string Value, string LabelKey);

    private sealed record SelectSetting(
        string LabelKey,
        Func<string?> Get,
        Action<string?> Set,
        SettingOption[] Options
    );

    private sealed record ToggleSetting(string LabelKey, Func<bool> Get, Action<bool> Set);

    private sealed record SettingGroup(
        string TitleKey,
        SelectSetting[] Selects,
        ToggleSetting[] Toggles
    );
}
