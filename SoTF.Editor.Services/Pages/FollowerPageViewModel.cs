using Newtonsoft.Json.Linq;
using NLog;
using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.Actors;
using SoTF.Editor.Data.Models.SaveData.Actor;
using SoTF.Editor.Data.Resources;
using SoTF.Editor.Data.ViewModels;
using SoTF.Editor.Services.Actors;
using SoTF.Editor.Services.Savegames;
using static SoTF.Editor.Data.Helpers.Constants.Actors;

namespace SoTF.Editor.Services.Pages;

public sealed class FollowerPageViewModel
{
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    private readonly AreaMaskManager _areaManager;
    private readonly GameData _gameData;

    // Every Kelvin And Virginia In The Save, Spawned Copies Included
    private readonly List<FollowerState> _followers = [];
    private readonly Dictionary<int, FollowerState> _selectedByTypeId = [];
    private readonly LoadedValues _loaded = new();
    private readonly Dictionary<int, int> _loadedCopies = [];
    private readonly HashSet<int> _loadedIds = [];
    private Savegame? _loadedFrom;

    public FollowerPageViewModel(GameData gameData)
    {
        _gameData = gameData;
        _areaManager = gameData.AreaManager;
        SelectFirstOfEachType();
    }

    public FollowerState KelvinState => _selectedByTypeId[KelvinTypeId];
    public FollowerState VirginiaState => _selectedByTypeId[VirginiaTypeId];

    public FollowerState StateOf(int typeId)
    {
        return typeId == KelvinTypeId ? KelvinState : VirginiaState;
    }

    public List<FollowerState> CopiesOf(int typeId)
    {
        return [.. _followers.Where(follower => follower.TypeId == typeId)];
    }

    public FollowerState? StateOfActor(int uniqueId)
    {
        return _followers.FirstOrDefault(follower => follower.UniqueId == uniqueId);
    }

    // A Spawned Or Removed Copy Changes The Ids
    public bool HasChanges =>
        _loaded.IsChanged(nameof(_followers), FollowerIds())
        || _followers.Concat(_selectedByTypeId.Values).Any(IsChanged);

    public IEnumerable<Change> Changes
    {
        get
        {
            foreach (var typeId in new[] { KelvinTypeId, VirginiaTypeId })
            {
                // A Follower Missing From The Save Is Its Stand In
                var copies = CopiesOf(typeId);
                var name = GameNames.Get(TextKeys.ActorType(typeId));
                var loadedCopies = _loadedCopies.GetValueOrDefault(typeId);
                if (copies.Count != loadedCopies)
                {
                    yield return new Change(
                        AppText.Format("ChangeCopies", name, copies.Count),
                        copies.Count > loadedCopies ? ChangeKind.Added : ChangeKind.Removed
                    );
                }

                var shown = copies.Count == 0 ? [StateOf(typeId)] : copies;
                for (var index = 0; index < shown.Count; index++)
                {
                    // A Spawned Copy Already Shows In The Copies Line
                    var isSpawned =
                        shown[index].UniqueId is { } uniqueId && !_loadedIds.Contains(uniqueId);
                    if (!isSpawned && IsChanged(shown[index]))
                    {
                        yield return new Change(shown.Count > 1 ? $"{name} {index + 1}" : name);
                    }
                }
            }
        }
    }

    private string FollowerIds()
    {
        return string.Join(",", _followers.Select(follower => follower.UniqueId));
    }

    public bool IsChanged(FollowerState follower)
    {
        return _loaded.AnyPropertyChanged(ScopeOf(follower), follower)
            || follower.Inventory.Any(item => IsChanged(follower, item))
            || InfluencesChanged(follower);
    }

    public bool IsChanged(FollowerState follower, string propertyName)
    {
        return _loaded.IsPropertyChanged(ScopeOf(follower), follower, propertyName);
    }

    public bool IsChanged(FollowerState follower, EquippableItem item)
    {
        return _loaded.IsChanged($"{ScopeOf(follower)}.Item.{item.ItemId}", item.Selected);
    }

    public bool IsChanged(FollowerState follower, Influence influence, string propertyName)
    {
        return _loaded.IsPropertyChanged(ScopeOf(follower, influence), influence, propertyName);
    }

    private bool InfluencesChanged(FollowerState follower)
    {
        return _loaded.IsChanged($"{ScopeOf(follower)}.Influences", InfluencesOf(follower));
    }

    private void Remember(FollowerState follower)
    {
        _loaded.RememberProperties(ScopeOf(follower), follower);
        foreach (var item in follower.Inventory)
        {
            _loaded.Remember($"{ScopeOf(follower)}.Item.{item.ItemId}", item.Selected);
        }

        _loaded.Remember($"{ScopeOf(follower)}.Influences", InfluencesOf(follower));
        foreach (var influence in follower.Influences)
        {
            _loaded.RememberProperties(ScopeOf(follower, influence), influence);
        }
    }

    private static string ScopeOf(FollowerState follower)
    {
        return $"{follower.TypeId}.{follower.UniqueId}";
    }

    private static string ScopeOf(FollowerState follower, Influence influence)
    {
        return $"{ScopeOf(follower)}.Influence.{influence.TypeId}";
    }

    private static string InfluencesOf(FollowerState follower)
    {
        return string.Join(
            "|",
            follower
                .Influences.OrderBy(influence => influence.TypeId)
                .Select(influence =>
                    LoadedValues.Format(
                        FormattableString.Invariant(
                            $"{influence.TypeId} {influence.Anger} {influence.Fear} {influence.Sentiment}"
                        )
                    )
                )
        );
    }

    public void Select(FollowerState follower)
    {
        _selectedByTypeId[follower.TypeId] = follower;
    }

    private void SelectFirstOfEachType()
    {
        // A Follower Missing From The Save Gets A Stand In
        foreach (var typeId in new[] { KelvinTypeId, VirginiaTypeId })
        {
            _selectedByTypeId[typeId] =
                _followers.FirstOrDefault(follower => follower.TypeId == typeId)
                ?? BuildFollowerState(_gameData, typeId);
        }
    }

    public static void FillAllBars(FollowerState followerState)
    {
        followerState.Health = 100;
        followerState.Fullness = 100;
        followerState.Hydration = 100;
        followerState.Energy = 100;
        followerState.Affection = 100;
    }

    private static FollowerState BuildFollowerState(GameData gameData, int typeId)
    {
        return new FollowerState(
            typeId,
            gameData.FollowerData.GetOutfits(typeId),
            gameData.FollowerData.GetEquippableItems(typeId, gameData.Items)
        );
    }

    public void Load(Savegame? selectedSavegame)
    {
        // An Actor Change Keeps The Copy Each Panel Shows
        List<int?> shown = ReferenceEquals(selectedSavegame, _loadedFrom)
            ? [.. _selectedByTypeId.Values.Select(follower => follower.UniqueId)]
            : [];
        _followers.Clear();
        if (selectedSavegame != null)
        {
            LoadFollowerData(selectedSavegame);
        }

        SelectFirstOfEachType();
        foreach (var follower in _followers.Where(follower => shown.Contains(follower.UniqueId)))
        {
            Select(follower);
        }
        LoadDeathFlags(selectedSavegame);

        // Actor Changes Reload The Same Savegame, Keep The First Load
        if (!ReferenceEquals(selectedSavegame, _loadedFrom))
        {
            _loadedFrom = selectedSavegame;
            _loaded.Clear();
            _loaded.Remember(nameof(_followers), FollowerIds());
            _loadedCopies[KelvinTypeId] = CopiesOf(KelvinTypeId).Count;
            _loadedCopies[VirginiaTypeId] = CopiesOf(VirginiaTypeId).Count;
            _loadedIds.Clear();
            _loadedIds.UnionWith(_followers.Select(follower => follower.UniqueId).OfType<int>());
            foreach (var follower in _followers.Concat(_selectedByTypeId.Values))
            {
                Remember(follower);
            }
        }
    }

    public static void MoveTo(FollowerState follower, Position target)
    {
        follower.Pos = target.ForTeleport();
    }

    private void LoadDeathFlags(Savegame? savegame)
    {
        // A Dead Follower Can Be Gone From The Actor List
        var gameState = savegame
            ?.SavegameStore.LoadJsonRaw(SavegameStore.FileType.GameStateSaveData)
            ?.GetJsonBasedToken(Constants.JsonKeys.GameState);
        var kelvinDead = gameState?["IsRobbyDead"]?.Value<bool>() ?? false;
        var virginiaDead = gameState?["IsVirginiaDead"]?.Value<bool>() ?? false;
        foreach (var follower in _followers.Append(KelvinState).Append(VirginiaState))
        {
            follower.IsMarkedDead = follower.TypeId == KelvinTypeId ? kelvinDead : virginiaDead;
        }
    }

    private void LoadFollowerData(Savegame savegame)
    {
        var saveDataWrapper = savegame.SavegameStore.LoadJsonRaw(SavegameStore.FileType.SaveData);

        if (
            saveDataWrapper?.GetJsonBasedToken(Constants.JsonKeys.VailWorldSim)
            is not JObject vailWorldSim
        )
        {
            return;
        }

        // One State Per Follower Actor, A Repeated Id Only Once
        var found = new HashSet<int>();
        foreach (var actor in vailWorldSim["Actors"] ?? Enumerable.Empty<JToken>())
        {
            if (
                actor["TypeId"]?.Value<int>()
                    is not ((KelvinTypeId or VirginiaTypeId) and var typeId)
                || actor["UniqueId"]?.Value<int>() is not { } uniqueId
                || !found.Add(uniqueId)
            )
            {
                continue;
            }

            var followerModel = BuildFollowerState(_gameData, typeId);
            followerModel.UniqueId = uniqueId;
            _followers.Add(followerModel);

            followerModel.State = actor["State"]?.Value<int>() ?? -1;

            if (actor["Position"]?.ToObject<Position>() is { } position)
            {
                // The Graph Mask Says Which Cave The Follower Is In
                followerModel.Pos = position.WithoutOffset(
                    actor["GraphMask"]?.Value<int>() is { } graphMask
                        ? _areaManager.GetAreaForGraphMask(graphMask)
                        : AreaMaskManager.Surface
                );
            }

            if (actor["Stats"] is { } stats)
            {
                followerModel.Health = stats["Health"]?.Value<float>() ?? 0f;
                followerModel.Anger = stats["Anger"]?.Value<float>() ?? 0f;
                followerModel.Fear = stats["Fear"]?.Value<float>() ?? 0f;
                followerModel.Fullness = stats["Fullness"]?.Value<float>() ?? 0f;
                followerModel.Hydration = stats["Hydration"]?.Value<float>() ?? 0f;
                followerModel.Energy = stats["Energy"]?.Value<float>() ?? 0f;
                followerModel.Affection = stats["Affection"]?.Value<float>() ?? 0f;
            }

            if (
                actor["EquippedItems"] is JArray equippedItems
                && equippedItems.ToObject<int[]>() is { } itemIds
            )
            {
                foreach (var itemId in itemIds)
                {
                    if (
                        followerModel.Inventory.FirstOrDefault(item => item.ItemId == itemId)
                        is not { } inventoryItem
                    )
                    {
                        s_logger.Warn(
                            $"Found Unknown Item {itemId} In Follower {typeId} Inventory"
                        );
                        var item = new Item { Id = itemId };
                        followerModel.Inventory.Add(new EquippableItem(item) { Selected = true });
                        continue;
                    }

                    inventoryItem.Selected = true;
                }
            }

            // The Game Leaves Out The Default Outfit
            var outfitId = actor["OutfitId"]?.Value<int>() ?? 0;
            followerModel.Outfit = followerModel.Outfits.FirstOrDefault(outfit =>
                outfit.Id == outfitId
            );
        }

        foreach (
            var influenceMemory in vailWorldSim["InfluenceMemory"] ?? Enumerable.Empty<JToken>()
        )
        {
            // A Repeated Memory Or Type Counts Once, Newest Memory Wins
            if (
                influenceMemory["UniqueId"]?.Value<int>() is { } uniqueId
                && StateOfActor(uniqueId) is { } followerState
            )
            {
                followerState.Influences.Clear();
                followerState.Influences.AddRange(
                    (influenceMemory["Influences"]?.ToObject<List<Influence>>() ?? []).DistinctBy(
                        influence => influence.TypeId
                    )
                );
            }
        }
    }

    public bool Update(Savegame? savegame)
    {
        if (
            savegame?.SavegameStore.LoadJsonRaw(SavegameStore.FileType.SaveData)
            is not { } saveDataWrapper
        )
        {
            return false;
        }

        return new FollowerModifier(saveDataWrapper).Update(_followers);
    }
}
