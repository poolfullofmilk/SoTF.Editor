using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.Actors;
using SoTF.Editor.Data.Models.Map;
using SoTF.Editor.Data.Resources;
using SoTF.Editor.Services.Pages;
using SoTF.Editor.Services.Savegames;
using SoTF.Editor.Services.Settings;

namespace SoTF.Editor.Services.Map;

public class MapViewModel(
    MainViewModel mainViewModel,
    MapManager mapManager,
    PlayerPageViewModel playerPage,
    FollowerPageViewModel followerPage,
    GameData gameData
)
{
    // What The Map Shows Before Any Choice, And After Reset
    private static readonly HashSet<string> s_defaultGroups =
    [
        nameof(PoiGroupType.Bunkers),
        nameof(PoiGroupType.Caves),
        nameof(PoiGroupType.Doors),
        nameof(PoiGroupType.Helicopters),
        nameof(PoiGroupType.Info),
        nameof(PoiGroupType.Laptops),
        nameof(PoiGroupType.Player),
        nameof(PoiGroupType.Printers),
        nameof(PoiGroupType.Villages),
        nameof(PoiGroupType.ZipLines),
        PoiGroupKeys.Actors + "_followers",
        PoiGroupKeys.WorldItems + "Knight V",
    ];

    private List<IPoiGrouper> _poiGroups = [];
    private int _builtRevision = -1;

    public MapFilter MapFilter { get; } = new(gameData.AreaManager);

    public IReadOnlyList<IPoiGrouper> PoiGroups
    {
        get
        {
            RebuildIfStale();
            return _poiGroups;
        }
    }

    public IPoi? SelectedPoi { get; private set; }

    // Points Under One Click, Picked From A List
    public IReadOnlyList<BasePoi> StackedPois { get; private set; } = [];

    // An Anchor Move Or New Zipline Waiting For Its Spot
    public BasePoi? PickFrom { get; private set; }
    public bool PicksNewZipline { get; private set; }

    public Position? FocusPosition { get; set; }

    public void Build()
    {
        _builtRevision = mainViewModel.Revision;
        _poiGroups = mapManager.BuildPoiGroups(mainViewModel.SelectedSavegame);
        LoadSavedSelectedPois();
        SelectPoi(null);
        ApplyFilterToAllPois();
    }

    public void Release()
    {
        // Points Hold Savegame JSON, Let Them Go With The Map
        _poiGroups = [];
        _builtRevision = -1;
        SelectPoi(null);
    }

    private void RebuildIfStale()
    {
        // Points From An Older Load Hold Stale JSON
        if (_builtRevision != mainViewModel.Revision)
        {
            Build();
        }
    }

    public IEnumerable<PoiGroup> AllGroups()
    {
        return _poiGroups.SelectMany(grouper =>
            grouper switch
            {
                PoiGroup poiGroup => [poiGroup],
                PoiGroupCollection poiGroupCollection => poiGroupCollection.PoiGroups,
                _ => Enumerable.Empty<PoiGroup>(),
            }
        );
    }

    public IEnumerable<IPoi> AllPois()
    {
        RebuildIfStale();
        return AllGroups().SelectMany(group => group.Pois);
    }

    public IEnumerable<IPoi> VisiblePois()
    {
        SyncLivePositions();
        // Stacking Comes From Each Icon's Z Index, Not The Order
        return AllPois().Where(poi => poi.Visible && poi is not ZiplinePoi);
    }

    public IEnumerable<ZiplinePoi> VisibleZiplines()
    {
        return AllPois().OfType<ZiplinePoi>().Where(zipline => zipline.Visible);
    }

    private void SyncLivePositions()
    {
        // Pages Move These While The Map Is Open
        foreach (var poi in AllPois())
        {
            switch (poi)
            {
                case PlayerPoi playerPoi:
                    playerPoi.Position = playerPage.PlayerState.Pos;
                    break;
                case ActorPoi { IsFollower: true } actorPoi:
                    actorPoi.Position =
                        followerPage.StateOfActor(actorPoi.Actor.UniqueId)?.Pos
                        ?? actorPoi.Position;
                    break;
            }
        }
    }

    public IPoi? TakeFocus()
    {
        // Show On Map Picks The Point Itself, Its Group Turned On And Filters Cleared
        if (FocusPosition is not { } position)
        {
            return null;
        }

        SyncLivePositions();
        if (
            AllPois()
                .OfType<BasePoi>()
                .Where(poi => poi.Position?.HasSameCoordinates(position) == true)
                .OrderBy(poi => poi is PlayerPoi or ActorPoi ? 0 : 1)
                .FirstOrDefault()
            is not { } focused
        )
        {
            return null;
        }

        FocusPosition = null;
        foreach (var group in AllGroups().Where(group => group.Pois.Contains(focused)))
        {
            group.Enabled = true;
        }

        SaveSettings();
        if (focused.Filtered)
        {
            MapFilter.Reset();
        }

        ApplyFilterToAllPois();
        SelectPoi(focused);
        return focused;
    }

    // A Group With Nothing Left After The Search And Filters
    public static bool HasMatches(PoiGroup group)
    {
        return group.Pois.OfType<BasePoi>().Any(poi => !poi.Filtered);
    }

    public void SelectPoi(IPoi? poi)
    {
        // A New Pick Ends Any List Or Waiting Zipline
        StackedPois = [];
        PickFrom = null;
        SelectedPoi?.IsSelected = false;
        SelectedPoi = poi;
        poi?.IsSelected = true;
    }

    public void Click(float left, float top, float zoom, bool spotOnly)
    {
        // Icons Under The Pointer, Topmost First, At Screen Size
        List<BasePoi> hits = spotOnly
            ? []
            :
            [
                .. VisiblePois()
                    .OfType<BasePoi>()
                    .Reverse()
                    .Where(poi =>
                        Math.Abs(poi.MapLeft - left) <= poi.IconSize / 2f / zoom
                        && Math.Abs(poi.MapTop - top) <= poi.IconSize / 2f / zoom
                    )
                    .OrderByDescending(poi => poi.IconZIndex),
            ];

        // A Waiting Zipline Takes The Point Clicked, Or The Spot
        if (PickFrom is { } from)
        {
            PickFrom = null;
            Place(
                from,
                hits.FirstOrDefault(poi => poi != from && poi.Position != null)?.Position
                    ?? SpotAt(left, top)
            );
            return;
        }

        SelectPoi(
            hits.Count switch
            {
                0 => new LocationPoi(SpotAt(left, top))
                {
                    Description = AppText.Get("HeightGuessed"),
                },
                1 => hits[0],
                _ => null,
            }
        );
        StackedPois = hits.Count > 1 ? hits : [];
    }

    public void TogglePick(BasePoi from, bool newZipline)
    {
        // The Same Button Again Stops Waiting
        PickFrom = IsPicking(from, newZipline) ? null : from;
        PicksNewZipline = newZipline;
    }

    public bool IsPicking(BasePoi from, bool newZipline)
    {
        return PickFrom == from && PicksNewZipline == newZipline;
    }

    private void Place(BasePoi from, Position spot)
    {
        if (mainViewModel.SelectedSavegame is not { } savegame || from.Position is not { } start)
        {
            return;
        }

        if (!PicksNewZipline && from is ZipPointPoi anchor)
        {
            ZiplineManager.MoveAnchor(anchor, savegame, spot);
        }
        else if (PicksNewZipline && ZiplineManager.CreateNew(savegame, start, spot) is { } token)
        {
            var zipline = new ZiplinePoi(token, start, spot);
            AddZipline(zipline);
            SelectPoi(zipline.PointB);
        }

        // A Moved Or New Zipline Lights The Map Menu
        mainViewModel.NotifyChanged();
    }

    private Position SpotAt(float left, float top)
    {
        var (x, z) = CoordinateConverter.PixelToIngame(left, top);

        // Ponytail: Nearest Known Height, A Heightmap Would Be Exact
        var nearest = AllPois()
            .Where(poi => poi is not (StructurePoi or ZipPointPoi))
            .Select(poi => poi.Position)
            .OfType<Position>()
            .Where(position => !position.Area.IsUnderground())
            .MinBy(position =>
                (position.X - x) * (position.X - x) + (position.Z - z) * (position.Z - z)
            );
        return new Position(x, nearest?.Y ?? 0, z);
    }

    private void AddZipline(ZiplinePoi zipline)
    {
        if (
            _poiGroups
                .OfType<PoiGroup>()
                .FirstOrDefault(group => group.GroupType == PoiGroupType.ZipLines)
            is not { } ziplineGroup
        )
        {
            return;
        }

        foreach (var newPoi in new IPoi[] { zipline.PointA, zipline.PointB, zipline })
        {
            newPoi.Enabled = ziplineGroup.Enabled;
            newPoi.ApplyFilter(MapFilter);
            ziplineGroup.Pois.Add(newPoi);
        }
    }

    public void RemoveZipline(ZiplinePoi zipline)
    {
        if (
            mainViewModel.SelectedSavegame is not { } selectedSavegame
            || !ZiplineManager.Remove(zipline, selectedSavegame)
        )
        {
            return;
        }

        RemovePois([zipline, zipline.PointA, zipline.PointB]);
        mainViewModel.NotifyChanged();
    }

    private void RemovePois(IReadOnlyCollection<IPoi> pois)
    {
        if (SelectedPoi != null && pois.Contains(SelectedPoi))
        {
            SelectPoi(null);
        }

        foreach (var poiGroup in AllGroups())
        {
            poiGroup.Pois.ExceptWith(pois);
        }
    }

    public void ApplyFilterToAllPois()
    {
        foreach (var poi in AllPois())
        {
            poi.ApplyFilter(MapFilter);
        }
    }

    public void ResetToDefault()
    {
        foreach (var group in AllGroups())
        {
            group.Enabled = IsDefault(group);
        }

        SaveSettings();
    }

    public bool IsAtDefault()
    {
        return AllGroups().All(group => group.Enabled == IsDefault(group));
    }

    private static bool IsDefault(PoiGroup group)
    {
        return s_defaultGroups.Contains(group.GroupKey);
    }

    public void SetAllEnabled(bool enabled)
    {
        foreach (var poiGrouper in _poiGroups)
        {
            poiGrouper.Enabled = enabled;
        }

        SaveSettings();
    }

    public void SaveSettings()
    {
        var selected = UserSettings.Current.SelectedMapGroups ?? [];

        foreach (var poiGrouper in _poiGroups)
        {
            switch (poiGrouper)
            {
                case PoiGroup poiGroup:
                    Toggle(selected, poiGroup.GroupKey, poiGroup.Enabled);
                    break;
                case PoiGroupCollection poiGroupCollection:
                    Toggle(selected, poiGroupCollection.GroupKey, poiGroupCollection.Enabled);

                    foreach (var group in poiGroupCollection.PoiGroups)
                    {
                        Toggle(selected, group.GroupKey, group.Enabled);
                    }

                    break;
            }
        }

        UserSettings.Current.SelectedMapGroups = selected;
        UserSettings.Current.Save();
    }

    private static void Toggle(HashSet<string> selected, string groupKey, bool enabled)
    {
        _ = enabled ? selected.Add(groupKey) : selected.Remove(groupKey);
    }

    private void LoadSavedSelectedPois()
    {
        var selectedGroups = UserSettings.Current.SelectedMapGroups;
        foreach (var group in AllGroups())
        {
            group.Enabled = selectedGroups?.Contains(group.GroupKey) ?? IsDefault(group);
        }
    }
}
