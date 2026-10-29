using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.Actors;
using SoTF.Editor.Data.Models.Map;
using SoTF.Editor.Data.Models.SaveData.Actor;
using SoTF.Editor.Services.Actors;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Services.Map;

public class MapSpawnActorsViewModel
{
    private readonly BasePoi _destination;

    public MapSpawnActorsViewModel(
        BasePoi destination,
        IList<int> allFamilyIds,
        List<ActorType> allActorTypes,
        AreaMaskManager areaMaskManager
    )
    {
        // Minus One Means No Family, Inserted Into Our Own Copy
        AllFamilyIds = [-1, .. allFamilyIds];
        AllActorTypes = destination.IsUnderground
            ? [.. allActorTypes.Where(actorType => !actorType.IsFollower())]
            : allActorTypes;
        _destination = destination;
        Landing = new Landing(destination, 1, areaMaskManager);

        if (destination is ActorPoi actorPoi)
        {
            OriginalFamilyId = FamilyId = actorPoi.Actor.FamilyId;
            SelectedActorType = actorPoi.Actor.ActorType;
        }
        else
        {
            OriginalFamilyId = FamilyId = -1;
        }
    }

    public int? FamilyId { get; set; }

    public ActorType? SelectedActorType { get; set; }

    public SpawnPattern SelectedSpawnPattern { get; set; } = SpawnPattern.Grid;

    public int SpaceBetween { get; set; }

    public int SpawnCount { get; set; } = 1;

    public int? OriginalFamilyId { get; }

    public List<int> AllFamilyIds { get; }
    public List<ActorType> AllActorTypes { get; }

    public List<Influence> Influences { get; } = [];

    public Landing Landing { get; }

    public string Destination => _destination.Title;

    public bool CanSpawn()
    {
        return SelectedActorType != null && SpawnCount > 0;
    }

    public void DoSpawn(Savegame savegame)
    {
        if (SelectedActorType == null)
        {
            return;
        }

        ActorModifier.Spawn(
            savegame,
            Landing.At(),
            SelectedActorType,
            SpawnCount,
            FamilyId == -1 ? null : FamilyId,
            [.. Influences],
            SpaceBetween,
            SelectedSpawnPattern
        );
    }
}
