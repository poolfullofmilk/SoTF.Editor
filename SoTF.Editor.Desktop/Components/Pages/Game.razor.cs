using Microsoft.AspNetCore.Components;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Desktop.Components.Shared.Common;
using SoTF.Editor.Services.Pages;
using static SoTF.Editor.Data.Helpers.Constants;
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

    // The Game Only Knows Normal And Hard Stat Regeneration
    private static readonly SettingOption[] s_normalHard =
    [
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

    private static readonly SettingOption[] s_lengths =
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

    // Each Setting With The Value The Game Assumes When Missing
    private static readonly SettingGroup[] s_setupGroups =
    [
        new(
            "CustomSettings",
            [
                new(
                    "ConsumableEffects",
                    GameSetupKeys.ConsumableEffects,
                    SettingValueNormal,
                    s_offNormalHard
                ),
                new(
                    "PlayerStatsDamage",
                    GameSetupKeys.PlayerStatsDamage,
                    SettingValueOff,
                    s_offNormalHard
                ),
                new(
                    "AnimalSpawnRate",
                    GameSetupKeys.AnimalSpawnRate,
                    SettingValueNORMAL,
                    s_lowNormalHigh
                ),
                new(
                    "ColdPenalties",
                    GameSetupKeys.ColdPenalties,
                    SettingValueNormal,
                    s_offNormalHard
                ),
                new(
                    "BuildingResistance",
                    GameSetupKeys.BuildingResistance,
                    SettingValueNORMAL,
                    s_lowNormalHigh
                ),
                new(
                    "StatRegenerationPenalties",
                    GameSetupKeys.StatRegenerationPenalty,
                    SettingValueNormal,
                    s_normalHard
                ),
            ],
            [
                new("InventoryPause", GameSetupKeys.PausedInInventory, false),
                new("ReducedFood", GameSetupKeys.ReducedFoodInContainers, false),
                new("ReducedAmmo", GameSetupKeys.ReducedAmmoInContainers, false),
                new("SingleUseContainers", GameSetupKeys.SingleUseContainers, false),
                new("PlayersTriggerTraps", GameSetupKeys.PlayersTriggerTraps, false),
                new("CreativeMode", GameSetupKeys.CreativeMode, false),
                new("GodMode", GameSetupKeys.PlayersImmortalMode, false),
                new("OneHitTrees", GameSetupKeys.OneHitToCutTrees, false),
                new("InstantBuilding", GameSetupKeys.ForcePlaceFullLoad, false),
                new("NoCuttingsSpawn", GameSetupKeys.NoCuttingsSpawn, false),
            ]
        ),
        new(
            "Enemies",
            [
                new("EnemyHealth", GameSetupKeys.EnemyHealth, SettingValueNORMAL, s_lowNormalHigh),
                new("EnemyDamage", GameSetupKeys.EnemyDamage, SettingValueNORMAL, s_lowNormalHigh),
                new("EnemyArmor", GameSetupKeys.EnemyArmour, SettingValueNORMAL, s_lowNormalHigh),
                new(
                    "EnemyAggression",
                    GameSetupKeys.EnemyAggression,
                    SettingValueNORMAL,
                    s_lowNormalHigh
                ),
                new(
                    "EnemySearchParties",
                    GameSetupKeys.EnemySearchParties,
                    SettingValueNORMAL,
                    s_lowNormalHigh
                ),
            ],
            [new("SpawnEnemies", GameSetupKeys.EnemySpawn, true)]
        ),
        new(
            "Weather",
            [
                new("StartingSeason", GameSetupKeys.StartingSeason, "Summer", s_seasons),
                new("SeasonLength", GameSetupKeys.SeasonLength, SettingValueDefault, s_lengths),
                new("DayLength", GameSetupKeys.DayLength, SettingValueDefault, s_lengths),
                new(
                    "PrecipitationFrequency",
                    GameSetupKeys.PrecipitationFrequency,
                    SettingValueDefault,
                    s_precipitation
                ),
            ],
            []
        ),
        new(
            "Multiplayer",
            [new("PvpDamage", GameSetupKeys.PvpDamage, SettingValueNormal, s_pvpDamage)],
            [new("MultiplayerCheats", GameSetupKeys.MultiplayerCheats, true)]
        ),
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

    private sealed record SettingOption(string Value, string LabelKey);

    private sealed record SelectSetting(
        string LabelKey,
        string Key,
        string Fallback,
        SettingOption[] Options
    );

    private sealed record ToggleSetting(string LabelKey, string Key, bool Fallback);

    private sealed record SettingGroup(
        string TitleKey,
        SelectSetting[] Selects,
        ToggleSetting[] Toggles
    );
}
