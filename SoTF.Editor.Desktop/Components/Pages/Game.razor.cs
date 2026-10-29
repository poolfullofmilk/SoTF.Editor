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
    private static readonly ModeCard[] s_modes =
    [
        new("Normal", "Normal", MudBlazor.Icons.Material.Rounded.Forest),
        new(HardGameMode, "Hard", MudBlazor.Icons.Material.Rounded.Whatshot),
        new(HardSurvivalGameMode, "HardSurvival", MudBlazor.Icons.Material.Rounded.AcUnit),
        new(PeacefulGameMode, "Peaceful", MudBlazor.Icons.Material.Rounded.Spa),
        new(CreativeGameMode, "ModeCreative", MudBlazor.Icons.Material.Rounded.Construction),
        new(CustomGameMode, "ModeCustom", MudBlazor.Icons.Material.Rounded.Tune),
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
            MudBlazor.Icons.Material.Rounded.Warning,
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
            MudBlazor.Icons.Material.Rounded.WbSunny,
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
            MudBlazor.Icons.Material.Rounded.Group,
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

    private bool ModeChanged => GameSetup.IsChanged(GameSetupKeys.Mode, CustomGameMode);

    private bool IsMode(ModeCard mode)
    {
        return GameSetup.SelectedMode == mode.Value;
    }

    private bool IsModeChanged(ModeCard mode)
    {
        return IsMode(mode) && ModeChanged;
    }

    private string ModeClass(ModeCard mode)
    {
        // The Picked Mode Wears The Accent Ring
        return "rounded-panel pa-4 hover-card flex-grow-1"
            + (IsMode(mode) ? " card-open" : "")
            + (IsModeChanged(mode) ? " card-changed" : "");
    }

    private static string SettingClass(bool changed)
    {
        // One Setting Per Card, Its Dot Sits On The Corner
        return "rounded-panel pa-4 flex-grow-1" + (changed ? " card-changed" : "");
    }

    private static string SeasonClass(List<DayMark> marks)
    {
        return "rounded-panel pa-4" + (marks.Exists(mark => mark.IsNew) ? " card-changed" : "");
    }

    private bool IsPicked(SelectSetting setting, SettingOption option)
    {
        return GameSetup.GetString(setting.Key, setting.Fallback) == option.Value;
    }

    private async Task LoadPresetAsync(string mode)
    {
        if (await ConfirmAsync("LoadPreset", "LoadPresetText"))
        {
            GameSetup.ApplyPreset(mode);
        }
    }

    private bool IsCustom => GameSetup.SelectedMode == CustomGameMode;

    private IEnumerable<GenericSetting> FilteredNamedIntDatas =>
        GameState
            .NamedIntDatas.Where(setting =>
                string.IsNullOrWhiteSpace(_namedIntFilter)
                || setting.Name.Contains(_namedIntFilter, StringComparison.OrdinalIgnoreCase)
            )
            .OrderBy(setting => setting.Name, StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<string> KeysOf(SettingGroup group)
    {
        return group
            .Selects.Select(setting => setting.Key)
            .Concat(group.Toggles.Select(toggle => toggle.Key));
    }

    private double? DaysPerSeason =>
        SeasonCalendar.DaysPerSeason(
            GameSetup.GetString(GameSetupKeys.SeasonLength, SettingValueDefault)
        );

    private double DaysPlayed =>
        (GameStateValue("GameDays") ?? 0) + ((GameStateValue("GameHours") ?? 0) / 24.0);

    // Season Follows Days Plus Offset, The Panel Moves It
    private SeasonState? Calendar => CalendarAt(Weather.Offset?.DecimalValue);

    private SeasonState? LoadedCalendar => CalendarAt(Weather.LoadedOffset);

    // A Known Season Length Shows The Panel, Not The Offset
    private IEnumerable<GenericSetting> WeatherRows =>
        Calendar is null
            ? Weather.Settings
            : Weather.Settings.Where(setting => setting != Weather.Offset);

    private SeasonState? CalendarAt(double? offset)
    {
        if (DaysPerSeason is not { } daysPerSeason || offset is not { } dayOffset)
        {
            return null;
        }

        var days = GameStateValue("GameDays") ?? 0;
        return new SeasonState(
            SeasonCalendar.SeasonAt(DaysPlayed, dayOffset, daysPerSeason),
            SeasonCalendar.DaysLeft(days, dayOffset, daysPerSeason),
            (int)daysPerSeason
        );
    }

    // Long Seasons Touch Their Cells Into One Bar
    private const int MaxDayCells = 15;

    // Wide Enough For Current Or New With Its Dot
    private const int MarkWidth = 56;

    // The Bar's Ends Lend A Label The Panel Padding
    private const int MarkEdge = 32;
    private const int MarkGap = 8;
    private const int MarkInset = 6;

    private static string? InfoOf(string settingName)
    {
        return TextKeys.SettingsWithInfo.Contains(settingName)
            ? AppText.Get(TextKeys.SettingInfo(settingName))
            : null;
    }

    private int? GameStateValue(string name)
    {
        return GameState.Settings.FirstOrDefault(setting => setting.Name == name)?.IntValue;
    }

    private void PickDay(int season, int day)
    {
        if (DaysPerSeason is not { } daysPerSeason || Weather.Offset is not { } offset)
        {
            return;
        }

        // From The Loaded Offset, Its Own Day Restores It Exactly
        offset.DecimalValue =
            LoadedCalendar is { } loaded && loaded.Season == season && loaded.Day == day
                ? Weather.LoadedOffset
                : SeasonCalendar.OffsetForDay(
                    season,
                    day,
                    DaysPlayed,
                    Weather.LoadedOffset,
                    daysPerSeason
                );
    }

    private static string SeasonName(int season)
    {
        return AppText.Get(s_seasons[season].LabelKey);
    }

    private static string DayTitle(int season, int day)
    {
        return $"{SeasonName(season)}, {AppText.Format("SavegameDay", day)}";
    }

    private static string DayClass(SeasonState calendar, SeasonState loaded, int season, int day)
    {
        // Filled Up To Today, Which Pulses, Today And Loaded Ringed
        var isToday = calendar.IsDay(season, day);
        return "season-day"
            + (calendar.IsDone(season, day) ? " done" : "")
            + (isToday ? " today" : "")
            + (isToday || loaded.IsDay(season, day) ? " marked" : "");
    }

    private static List<DayMark> MarksOf(SeasonState calendar, SeasonState loaded)
    {
        // Current On The Loaded Day, New Once It Moved
        List<DayMark> marks = [new(loaded.Season, loaded.Day, AppText.Get("SeasonCurrent"), false)];
        if (!calendar.IsDay(loaded.Season, loaded.Day))
        {
            marks.Add(new(calendar.Season, calendar.Day, AppText.Get("SeasonNew"), true));
        }

        return marks;
    }

    private static string MarkStyle(DayMark mark, List<DayMark> marks, int length)
    {
        var other = marks.Find(candidate => candidate != mark);
        var start = (mark.Day - 1) * 100.0 / length;
        var end = mark.Day * 100.0 / length;

        // Alone A Label Sits Centred Over Its Day
        if (other is null)
        {
            var centred = FormattableString.Invariant(
                $"calc({(start + end) / 2}% - {MarkWidth / 2}px)"
            );
            var left = mark.Season switch
            {
                0 => $"max(-12px, {centred})",
                3 => $"min(calc(100% - {MarkWidth - 12}px), {centred})",
                _ => centred,
            };
            return $"left:{left};width:{MarkWidth}px;text-align:center";
        }

        // Two Lean Apart, So They Never Touch
        if ((mark.Season, mark.Day).CompareTo((other.Season, other.Day)) < 0)
        {
            var edge = FormattableString.Invariant($"calc({end}% - {MarkInset}px)");
            var right = mark.Season switch
            {
                0 => $"max({MarkEdge}px, {edge})",
                3 => $"min(calc(100% - {MarkEdge + MarkGap}px), {edge})",
                _ => edge,
            };
            return $"right:calc(100% - {right})";
        }

        var begin = FormattableString.Invariant($"calc({start}% + {MarkInset}px)");
        var from = mark.Season switch
        {
            0 => $"max({MarkEdge + MarkGap}px, {begin})",
            3 => $"min(calc(100% - {MarkEdge}px), {begin})",
            _ => begin,
        };
        return $"left:{from}";
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

    private sealed record ModeCard(string Value, string LabelKey, string Icon)
    {
        public string TextKey => LabelKey + "Text";

        public bool IsPreset => Value != CustomGameMode;
    }

    private sealed record SeasonState(int Season, int DaysLeft, int Length)
    {
        public int NextSeason => (Season + 1) % 4;

        public int Day => Length - DaysLeft + 1;

        public bool IsDone(int season, int day)
        {
            return season < Season || (season == Season && day <= Day);
        }

        public bool IsDay(int season, int day)
        {
            return season == Season && day == Day;
        }
    }

    private sealed record DayMark(int Season, int Day, string Label, bool IsNew);

    private sealed record SelectSetting(
        string LabelKey,
        string Key,
        string Fallback,
        SettingOption[] Options
    )
    {
        public string InfoKey => LabelKey + "Info";
    }

    private sealed record ToggleSetting(string LabelKey, string Key, bool Fallback)
    {
        public string InfoKey => LabelKey + "Info";
    }

    private sealed record SettingGroup(
        string TitleKey,
        string Icon,
        SelectSetting[] Selects,
        ToggleSetting[] Toggles
    );
}
