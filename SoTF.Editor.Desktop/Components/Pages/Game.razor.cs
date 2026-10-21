using Microsoft.AspNetCore.Components;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Resources;
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
            MudBlazor.Icons.Material.Rounded.Tune,
            [
                new(
                    "ConsumableEffects",
                    GameSetupKeys.ConsumableEffects,
                    SettingValueNormal,
                    s_offNormalHard,
                    "ConsumableEffectsInfo"
                ),
                new(
                    "PlayerStatsDamage",
                    GameSetupKeys.PlayerStatsDamage,
                    SettingValueOff,
                    s_offNormalHard,
                    "PlayerStatsDamageInfo"
                ),
                new(
                    "AnimalSpawnRate",
                    GameSetupKeys.AnimalSpawnRate,
                    SettingValueNORMAL,
                    s_lowNormalHigh,
                    "AnimalSpawnRateInfo"
                ),
                new(
                    "ColdPenalties",
                    GameSetupKeys.ColdPenalties,
                    SettingValueNormal,
                    s_offNormalHard,
                    "ColdPenaltiesInfo"
                ),
                new(
                    "BuildingResistance",
                    GameSetupKeys.BuildingResistance,
                    SettingValueNORMAL,
                    s_lowNormalHigh,
                    "BuildingResistanceInfo"
                ),
                new(
                    "StatRegenerationPenalties",
                    GameSetupKeys.StatRegenerationPenalty,
                    SettingValueNormal,
                    s_normalHard,
                    "StatRegenerationPenaltiesInfo"
                ),
            ],
            [
                new("InventoryPause", GameSetupKeys.PausedInInventory, false, "InventoryPauseInfo"),
                new("ReducedFood", GameSetupKeys.ReducedFoodInContainers, false, "ReducedFoodInfo"),
                new("ReducedAmmo", GameSetupKeys.ReducedAmmoInContainers, false, "ReducedAmmoInfo"),
                new(
                    "SingleUseContainers",
                    GameSetupKeys.SingleUseContainers,
                    false,
                    "SingleUseContainersInfo"
                ),
                new(
                    "PlayersTriggerTraps",
                    GameSetupKeys.PlayersTriggerTraps,
                    false,
                    "PlayersTriggerTrapsInfo"
                ),
                new("CreativeMode", GameSetupKeys.CreativeMode, false, "CreativeModeInfo"),
                new("GodMode", GameSetupKeys.PlayersImmortalMode, false, "GodModeInfo"),
                new("OneHitTrees", GameSetupKeys.OneHitToCutTrees, false, "OneHitTreesInfo"),
                new(
                    "InstantBuilding",
                    GameSetupKeys.ForcePlaceFullLoad,
                    false,
                    "InstantBuildingInfo"
                ),
                new("NoCuttingsSpawn", GameSetupKeys.NoCuttingsSpawn, false, "NoCuttingsSpawnInfo"),
            ]
        ),
        new(
            "Enemies",
            MudBlazor.Icons.Material.Rounded.Warning,
            [
                new(
                    "EnemyHealth",
                    GameSetupKeys.EnemyHealth,
                    SettingValueNORMAL,
                    s_lowNormalHigh,
                    "EnemyHealthInfo"
                ),
                new(
                    "EnemyDamage",
                    GameSetupKeys.EnemyDamage,
                    SettingValueNORMAL,
                    s_lowNormalHigh,
                    "EnemyDamageInfo"
                ),
                new(
                    "EnemyArmor",
                    GameSetupKeys.EnemyArmour,
                    SettingValueNORMAL,
                    s_lowNormalHigh,
                    "EnemyArmorInfo"
                ),
                new(
                    "EnemyAggression",
                    GameSetupKeys.EnemyAggression,
                    SettingValueNORMAL,
                    s_lowNormalHigh,
                    "EnemyAggressionInfo"
                ),
                new(
                    "EnemySearchParties",
                    GameSetupKeys.EnemySearchParties,
                    SettingValueNORMAL,
                    s_lowNormalHigh,
                    "EnemySearchPartiesInfo"
                ),
            ],
            [new("SpawnEnemies", GameSetupKeys.EnemySpawn, true, "SpawnEnemiesInfo")]
        ),
        new(
            "Weather",
            MudBlazor.Icons.Material.Rounded.WbSunny,
            [
                new(
                    "StartingSeason",
                    GameSetupKeys.StartingSeason,
                    "Summer",
                    s_seasons,
                    "StartingSeasonInfo"
                ),
                new(
                    "SeasonLength",
                    GameSetupKeys.SeasonLength,
                    SettingValueDefault,
                    s_lengths,
                    "SeasonLengthInfo"
                ),
                new(
                    "DayLength",
                    GameSetupKeys.DayLength,
                    SettingValueDefault,
                    s_lengths,
                    "DayLengthInfo"
                ),
                new(
                    "PrecipitationFrequency",
                    GameSetupKeys.PrecipitationFrequency,
                    SettingValueDefault,
                    s_precipitation,
                    "PrecipitationFrequencyInfo"
                ),
            ],
            []
        ),
        new(
            "Multiplayer",
            MudBlazor.Icons.Material.Rounded.Group,
            [
                new(
                    "PvpDamage",
                    GameSetupKeys.PvpDamage,
                    SettingValueNormal,
                    s_pvpDamage,
                    "PvpDamageInfo"
                ),
            ],
            [
                new(
                    "MultiplayerCheats",
                    GameSetupKeys.MultiplayerCheats,
                    true,
                    "MultiplayerCheatsInfo"
                ),
            ]
        ),
    ];

    [Inject]
    public required GameSetupPageViewModel GameSetup { get; set; }

    [Inject]
    public required GameStatePageViewModel GameState { get; set; }

    [Inject]
    public required WeatherPageViewModel Weather { get; set; }

    private string? _namedIntFilter;

    private bool ModeChanged => GameSetup.IsChanged(GameSetupKeys.Mode, CustomGameMode);

    // Season Changes With The Offset, Picking One Moves It
    private bool SeasonChanged => OffsetSetting is { } offset && Weather.IsChanged(offset);

    private static IEnumerable<SettingOption> Presets =>
        s_modes.Where(mode => mode.Value != CustomGameMode);

    private async Task LoadPresetAsync(string mode)
    {
        if (await ConfirmAsync("LoadPreset", "LoadPresetText"))
        {
            GameSetup.ApplyPreset(mode);
        }
    }

    private bool IsCustom => GameSetup.SelectedMode == CustomGameMode;

    private IEnumerable<GenericSetting> FilteredNamedIntDatas =>
        string.IsNullOrWhiteSpace(_namedIntFilter)
            ? GameState.NamedIntDatas
            : GameState.NamedIntDatas.Where(setting =>
                setting.Name.Contains(_namedIntFilter, StringComparison.OrdinalIgnoreCase)
            );

    private static IEnumerable<string> KeysOf(SettingGroup group)
    {
        return group
            .Selects.Select(setting => setting.Key)
            .Concat(group.Toggles.Select(toggle => toggle.Key));
    }

    private string LabelOf(IEnumerable<SettingOption> options, string? value)
    {
        return options.FirstOrDefault(option => option.Value == value) is { } option
            ? Localizer[option.LabelKey]
            : value ?? string.Empty;
    }

    // The Season Follows Days Plus Offset, Picking Moves The Offset
    private GenericSetting? OffsetSetting =>
        Weather.Settings.FirstOrDefault(setting => setting.Name == "_startingDayOffset");

    private double? DaysPerSeason =>
        SeasonCalendar.DaysPerSeason(
            GameSetup.GetString(GameSetupKeys.SeasonLength, SettingValueDefault)
        );

    private double DaysPlayed =>
        (GameStateValue("GameDays") ?? 0) + ((GameStateValue("GameHours") ?? 0) / 24.0);

    private string? CurrentSeason =>
        DaysPerSeason is { } daysPerSeason && OffsetSetting is { } offset
            ? s_seasons[
                SeasonCalendar.SeasonAt(DaysPlayed, offset.DecimalValue ?? 0, daysPerSeason)
            ].Value
            : null;

    private string? InfoOf(string settingName)
    {
        return TextKeys.SettingsWithInfo.Contains(settingName)
            ? Localizer[TextKeys.SettingInfo(settingName)].Value
            : null;
    }

    private int? GameStateValue(string name)
    {
        return GameState.Settings.FirstOrDefault(setting => setting.Name == name)?.IntValue;
    }

    private void ChangeSeason(string? season)
    {
        var index = Array.FindIndex(s_seasons, option => option.Value == season);
        if (index < 0 || DaysPerSeason is not { } daysPerSeason || OffsetSetting is not { } offset)
        {
            return;
        }

        offset.DecimalValue = SeasonCalendar.OffsetFor(
            index,
            DaysPlayed,
            offset.DecimalValue ?? 0,
            daysPerSeason
        );
    }

    public static string SetupLabelKey(string key)
    {
        // The Game Page Label, The Mode Has Its Own
        return s_setupGroups
                .SelectMany(group =>
                    group
                        .Selects.Select(setting => (setting.Key, setting.LabelKey))
                        .Concat(group.Toggles.Select(toggle => (toggle.Key, toggle.LabelKey)))
                )
                .FirstOrDefault(setting => setting.Key == key)
                .LabelKey
            ?? (key == GameSetupKeys.Mode ? "Mode" : key);
    }

    private sealed record SettingOption(string Value, string LabelKey);

    private sealed record SelectSetting(
        string LabelKey,
        string Key,
        string Fallback,
        SettingOption[] Options,
        string InfoKey
    );

    private sealed record ToggleSetting(string LabelKey, string Key, bool Fallback, string InfoKey);

    private sealed record SettingGroup(
        string TitleKey,
        string Icon,
        SelectSetting[] Selects,
        ToggleSetting[] Toggles
    );
}
