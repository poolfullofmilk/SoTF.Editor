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
        X = destination.Position?.X ?? destination.X;
        Y = destination.Position?.Y ?? 0;
        Z = destination.Position?.Z ?? destination.Z;

        if (destination is ActorPoi actorPoi)
        {
            OriginalFamilyId = FamilyId = actorPoi.Actor.FamilyId;
            SelectedActorType = actorPoi.Actor.ActorType;
        }
        else
        {
            OriginalFamilyId = FamilyId = -1;
        }

        Areas = [.. areaMaskManager.GetAllAreas().OrderBy(area => area.Name)];
        SelectedArea = destination.Position?.Area ?? AreaMaskManager.Surface;
    }

    public int? FamilyId { get; set; }

    public ActorType? SelectedActorType { get; set; }

    public Area SelectedArea { get; set; }

    public SpawnPattern SelectedSpawnPattern { get; set; } = SpawnPattern.Grid;

    public int SpaceBetween { get; set; }

    public int SpawnCount { get; set; } = 1;

    public float XOffset { get; set; } = 1;

    public float YOffset { get; set; } = 1;

    public float ZOffset { get; set; } = 1;

    public int? OriginalFamilyId { get; }

    public List<int> AllFamilyIds { get; }
    public List<ActorType> AllActorTypes { get; }

    public List<Influence> Influences { get; } = [];

    public float X { get; }
    public float Y { get; }
    public float Z { get; }

    public string Destination => _destination.Title;

    public List<Area> Areas { get; }

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

        // The Picked Area Counts, It Was Ignored Before
        var newPosition = new Position(X + XOffset, Y + YOffset, Z + ZOffset)
        {
            Area = SelectedArea,
        };

        ActorModifier.Spawn(
            savegame,
            newPosition,
            SelectedActorType,
            SpawnCount,
            FamilyId == -1 ? null : FamilyId,
            [.. Influences],
            SpaceBetween,
            SelectedSpawnPattern
        );
    }
}
