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

public class MapViewModel
{
    // What The Map Shows Before Any Choice, And After Reset
    private static readonly HashSet<string> s_defaultGroups =
    [
        nameof(PoiGroupType.Bunkers),
        nameof(PoiGroupType.Caves),
        nameof(PoiGroupType.Doors),
        nameof(PoiGroupType.Helicopters),
        nameof(PoiGroupType.Laptops),
        nameof(PoiGroupType.Player),
        nameof(PoiGroupType.Printers),
        nameof(PoiGroupType.Villages),
        nameof(PoiGroupType.ZipLines),
        PoiGroupKeys.Actors + "_followers",
        PoiGroupKeys.WorldItems + "Knight V",
    ];

    private readonly MainViewModel _mainViewModel;
    private readonly MapManager _mapManager;
    private readonly PoiLoader _poiLoader;
    private readonly PlayerPageViewModel _playerPage;
    private readonly FollowerPageViewModel _followerPage;
    private readonly StructuresPageViewModel _structuresPage;

    private List<IPoiGrouper> _poiGroups = [];

    // What The Points Were Built From, Reused While Unchanged
    private int _builtRevision = -1;
    private int _builtStructures;
    private HashSet<int> _builtOwnedItems = [];

    public MapViewModel(
        MainViewModel mainViewModel,
        MapManager mapManager,
        PoiLoader poiLoader,
        PlayerPageViewModel playerPage,
        FollowerPageViewModel followerPage,
        StructuresPageViewModel structuresPage,
        GameData gameData
    )
    {
        _mainViewModel = mainViewModel;
        _mapManager = mapManager;
        _poiLoader = poiLoader;
        _playerPage = playerPage;
        _followerPage = followerPage;
        _structuresPage = structuresPage;
        MapFilter = new MapFilter(gameData.AreaManager);

        // Points Of An Older Load Hold Its JSON
        mainViewModel.Changed += ReleaseIfStale;
    }

    public MapFilter MapFilter { get; }

    // Moves When A Point Shows, Hides Or Moves
    public int PointsVersion { get; private set; }

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

    // Each Visit Keeps The Points While Nothing They Show Changed
    public void Open()
    {
        var ownedItems = _poiLoader.OwnedItemIds();
        if (
            _builtRevision != _mainViewModel.Revision
            || _builtStructures != StructuresStamp()
            || !_builtOwnedItems.SetEquals(ownedItems)
        )
        {
            Build(ownedItems);
        }
        else
        {
            SelectPoi(null);
        }
    }

    public void Build()
    {
        Build(_poiLoader.OwnedItemIds());
    }

    private void Build(HashSet<int> ownedItems)
    {
        _builtRevision = _mainViewModel.Revision;
        _builtStructures = StructuresStamp();
        _builtOwnedItems = ownedItems;
        _poiGroups = _mapManager.BuildPoiGroups(_mainViewModel.SelectedSavegame, ownedItems);
        LoadSavedSelectedPois();
        SelectPoi(null);
        ApplyFilterToAllPois();
    }

    public void Close()
    {
        // The Points Stay For The Next Visit, A Pick Ends
        SelectPoi(null);
    }

    private void ReleaseIfStale()
    {
        if (_builtRevision != -1 && _builtRevision != _mainViewModel.Revision)
        {
            _poiGroups = [];
            _builtRevision = -1;
            SelectPoi(null);
        }
    }

    private int StructuresStamp()
    {
        // A Type Change Regroups Its Point, So Types Count
        var stamp = new HashCode();
        foreach (var structure in _structuresPage.Structures)
        {
            stamp.Add(structure.ScrewStructure?.Id ?? -1);
        }

        return stamp.ToHashCode();
    }

    private void RebuildIfStale()
    {
        // Points From An Older Load Hold Stale JSON
        if (_builtRevision != _mainViewModel.Revision)
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
        // Stacking Comes From Each Icon's Z Index, Not The Order
        return AllPois().Where(poi => poi.Visible && poi is not ZiplinePoi);
    }

    // The Version The Points Layer Renders, Pages Moved Some Since
    public int SyncPoints()
    {
        RebuildIfStale();
        SyncLivePositions();
        return PointsVersion;
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
            var livePosition = poi switch
            {
                PlayerPoi => _playerPage.PlayerState.Pos,
                ActorPoi { IsFollower: true } actorPoi => _followerPage
                    .StateOfActor(actorPoi.Actor.UniqueId)
                    ?.Pos,
                _ => null,
            };
            if (livePosition is null || poi is not BasePoi point)
            {
                continue;
            }

            var before = (point.MapLeft, point.MapTop, point.IconRotation);
            point.Position = livePosition;
            if (before != (point.MapLeft, point.MapTop, point.IconRotation))
            {
                PointsVersion++;
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
            (
                AllPois()
                    .OfType<BasePoi>()
                    .Where(poi => poi.Position?.HasSameCoordinates(position) == true)
                    .OrderBy(poi => poi is PlayerPoi or ActorPoi ? 0 : 1)
                    .FirstOrDefault()
                ?? ZiplineAt(position)?.PointA
            )
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

    private ZiplinePoi? ZiplineAt(Position position)
    {
        // A Zipline Structure Sits At The Middle Of Its Line
        return AllPois()
            .OfType<ZiplinePoi>()
            .FirstOrDefault(zipline =>
                zipline.PointA.Position is { } anchorA
                && zipline.PointB.Position is { } anchorB
                && Math.Abs((anchorA.X + anchorB.X) / 2 - position.X) < 0.01
                && Math.Abs((anchorA.Y + anchorB.Y) / 2 - position.Y) < 0.01
                && Math.Abs((anchorA.Z + anchorB.Z) / 2 - position.Z) < 0.01
            );
    }

    // Every Point The Search Finds, Shown Or Not
    public List<MapSearchResult> SearchResults()
    {
        if (MapFilter.NormalizedLowercaseFullText?.Trim() is not { Length: >= 2 })
        {
            return [];
        }

        return
        [
            .. AllGroups()
                .SelectMany(group =>
                    group
                        .Pois.OfType<BasePoi>()
                        .Where(poi => !poi.Filtered)
                        .Select(poi => new MapSearchResult(poi, group))
                )
                .OrderBy(result => result.Poi.Title, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(result => result.Group.Title, StringComparer.CurrentCultureIgnoreCase),
        ];
    }

    // A Picked Result Shows For Good And Opens
    public void FocusOn(BasePoi poi)
    {
        foreach (var group in AllGroups().Where(group => group.Pois.Contains(poi)))
        {
            group.Enabled = true;
        }

        SaveSettings();
        ApplyFilterToAllPois();
        SelectPoi(poi);
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
                    .OrderByDescending(poi => poi.IsSelected)
                    .ThenByDescending(poi => poi.IconZIndex),
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
        if (_mainViewModel.SelectedSavegame is not { } savegame || from.Position is not { } start)
        {
            return;
        }

        if (!PicksNewZipline && from is ZipPointPoi anchor)
        {
            ZiplineManager.MoveAnchor(anchor, savegame, spot);
            PointsVersion++;
        }
        else if (PicksNewZipline && ZiplineManager.CreateNew(savegame, start, spot) is { } token)
        {
            var zipline = new ZiplinePoi(token, start, spot);
            AddZipline(zipline);
            SelectPoi(zipline.PointB);
        }

        // A Moved Or New Zipline Lights The Map Menu
        _mainViewModel.NotifyChanged();
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
        // A Save Without Ziplines Gets Its Group Now
        if (
            _poiGroups
                .OfType<PoiGroup>()
                .FirstOrDefault(group => group.GroupType == PoiGroupType.ZipLines)
            is not { } ziplineGroup
        )
        {
            ziplineGroup = MapManager.ZiplineGroup([]);
            _poiGroups.Add(ziplineGroup);
        }

        ziplineGroup.Pois.UnionWith([zipline.PointA, zipline.PointB, zipline]);

        // A New Zipline Shows, Its Group Turned On For Good
        ziplineGroup.Enabled = true;
        SaveSettings();
        zipline.ApplyFilter(MapFilter);
        PointsVersion++;
    }

    public void RemoveZipline(ZiplinePoi zipline)
    {
        if (
            _mainViewModel.SelectedSavegame is not { } selectedSavegame
            || !ZiplineManager.Remove(zipline, selectedSavegame)
        )
        {
            return;
        }

        RemovePois([zipline, zipline.PointA, zipline.PointB]);
        _mainViewModel.NotifyChanged();
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

        PointsVersion++;
    }

    public void ApplyFilterToAllPois()
    {
        foreach (var poi in AllPois())
        {
            poi.ApplyFilter(MapFilter);
        }

        // Every Toggle, Search And Filter Ends Here
        PointsVersion++;
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
        foreach (var group in AllGroups())
        {
            group.Enabled = IsShown(group);
        }
    }

    private static bool IsShown(PoiGroup group)
    {
        return UserSettings.Current.SelectedMapGroups?.Contains(group.GroupKey) ?? IsDefault(group);
    }
}

public sealed record MapSearchResult(BasePoi Poi, PoiGroup Group);
