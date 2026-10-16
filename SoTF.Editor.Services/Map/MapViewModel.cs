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
    PlayerPageViewModel playerPage,
    FollowerPageViewModel followerPage,
    GameData gameData
)
{
    private List<IPoiGrouper> _poiGroups = [];

    public MapFilter MapFilter { get; } = new(gameData.AreaManager);

    public IReadOnlyList<IPoiGrouper> PoiGroups => _poiGroups;

    public IPoi? SelectedPoi { get; private set; }

    public Position? FocusPosition { get; set; }

    public static bool DarkMap
    {
        get => UserSettings.Current.DarkMap;
        set
        {
            UserSettings.Current.DarkMap = value;
            UserSettings.Current.Save();
        }
    }

    public static string MapImageUrl => DarkMap ? "images/map/dark.jpg" : "images/map/bright.jpg";

    public void Build()
    {
        var poiGrouperList = mainViewModel.BuildPoiGroups();
        LoadSavedSelectedPois(poiGrouperList);
        _poiGroups = poiGrouperList;
        SelectedPoi = null;
        ApplyFilterToAllPois();
    }

    public IEnumerable<IPoi> AllPois()
    {
        return _poiGroups.SelectMany(grouper =>
            grouper switch
            {
                PoiGroup poiGroup => poiGroup.Pois,
                PoiGroupCollection poiGroupCollection => poiGroupCollection.PoiGroups.SelectMany(
                    group => group.Pois
                ),
                _ => [],
            }
        );
    }

    public IEnumerable<IPoi> VisiblePois()
    {
        SyncLivePositions();
        return AllPois()
            .Where(poi => poi.Visible && poi is not ZiplinePoi)
            .OrderBy(poi => poi.IconZIndex);
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
                    actorPoi.Position = FollowerOf(actorPoi.Actor.TypeId).Pos;
                    break;
            }
        }
    }

    private FollowerState FollowerOf(int typeId)
    {
        return typeId == Constants.Actors.KelvinTypeId
            ? followerPage.KelvinState
            : followerPage.VirginiaState;
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
            newPoi.SetEnabledNoRefresh(ziplineGroup.Enabled);
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
    }

    private void RemovePois(IReadOnlyCollection<IPoi> pois)
    {
        if (SelectedPoi != null && pois.Contains(SelectedPoi))
        {
            SelectedPoi = null;
        }

        foreach (var poiGroup in _poiGroups.OfType<PoiGroup>())
        {
            poiGroup.Pois.ExceptWith(pois);
        }

        foreach (
            var poiGroup in _poiGroups
                .OfType<PoiGroupCollection>()
                .SelectMany(collection => collection.PoiGroups)
        )
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

    public void DeselectAll()
    {
        foreach (var poiGrouper in _poiGroups)
        {
            poiGrouper.SetEnabledNoRefresh(false);
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
        if (enabled)
        {
            selected.Add(groupKey);
        }
        else
        {
            selected.Remove(groupKey);
        }
    }

    private static void LoadSavedSelectedPois(List<IPoiGrouper> poiGrouperList)
    {
        if (UserSettings.Current.SelectedMapGroups is not { } selectedGroups)
        {
            return;
        }

        foreach (var poiGrouper in poiGrouperList)
        {
            switch (poiGrouper)
            {
                case PoiGroup poiGroup:
                    poiGroup.SetEnabledNoRefresh(selectedGroups.Contains(poiGroup.GroupKey));
                    break;
                case PoiGroupCollection poiGroupCollection:
                    foreach (var group in poiGroupCollection.PoiGroups)
                    {
                        group.SetEnabledNoRefresh(selectedGroups.Contains(group.GroupKey));
                    }

                    break;
            }
        }
    }
}
