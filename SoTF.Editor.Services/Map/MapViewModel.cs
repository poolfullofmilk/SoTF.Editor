using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.Actors;
using SoTF.Editor.Data.Models.Map;
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

    public Position? FocusPosition { get; set; }

    public void Build()
    {
        _builtRevision = mainViewModel.Revision;
        _poiGroups = mapManager.BuildPoiGroups(mainViewModel.SelectedSavegame);
        LoadSavedSelectedPois();
        SelectedPoi = null;
        ApplyFilterToAllPois();
    }

    public void Release()
    {
        // Points Hold Savegame JSON, Let Them Go With The Map
        _poiGroups = [];
        _builtRevision = -1;
        SelectedPoi = null;
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

    public void SelectPoi(IPoi? poi)
    {
        if (SelectedPoi is { } selectedPoi)
        {
            selectedPoi.IsSelected = false;

            // A Pending Zipline Move Or Creation Takes The Clicked Position
            if (
                selectedPoi is ZipPointPoi { IsMoveRequested: true } zipPointPoi
                && poi is { Position: { } position }
                && mainViewModel.SelectedSavegame is { } selectedSavegame
            )
            {
                if (
                    ZiplineManager.AcceptNewPosition(zipPointPoi, selectedSavegame, position) is
                    { } newZipline
                )
                {
                    AddZipline(newZipline);
                    poi = newZipline.PointB;
                }

                // A Moved Or New Zipline Lights The Map Menu
                mainViewModel.NotifyChanged();
            }
        }

        SelectedPoi = poi;
        poi?.IsSelected = true;
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
            SelectedPoi = null;
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
