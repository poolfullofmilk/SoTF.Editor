using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models.Actors;
using SoTF.Editor.Data.Models.SaveData.Actor;

namespace SoTF.Editor.Data.ViewModels;

public class EditActorViewModel
{
    public EditActorViewModel(Actor actor, List<ActorType> allActorTypes)
    {
        Actor = actor;
        AllActorTypes = allActorTypes;
        ModifyOptions.ActorEnergy = Actor.Stats?.GetValueOrDefault("Energy", 100f) ?? 100f;
        ModifyOptions.ActorHealth = Actor.Stats?.GetValueOrDefault("Health", 100f) ?? 100f;

        // Copies, So Closing Without Apply Leaves The Actor Untouched
        Influences =
        [
            .. (actor.Influences ?? []).Select(influence => new Influence
            {
                TypeId = influence.TypeId,
                Anger = influence.Anger,
                Fear = influence.Fear,
                Sentiment = influence.Sentiment,
            }),
        ];
    }

    // This Actor, Its Family, Its Type Or Everyone
    public short ActorSelection { get; set; }

    public ActorModificationMode ModificationMode { get; set; } = ActorModificationMode.Modify;

    public bool OnlyInSameAreaAsActor { get; set; } = true;

    public bool SkipFollowers { get; set; } = true;

    public List<Influence> Influences { get; }

    public ModifyOptions ModifyOptions { get; } = new();

    public Actor Actor { get; }

    public List<ActorType> AllActorTypes { get; }

    // Family Zero Means None, Matching It Meant Every Loner
    public bool HasFamily => Actor.FamilyId is not (null or 0);

    public bool Matches(
        int? typeId,
        int? familyId,
        int? graphMask,
        int? uniqueId,
        short? selection = null
    )
    {
        // Followers And Other Areas Skipped When Asked, Never Itself
        var isSelf = uniqueId == Actor.UniqueId;
        if (
            !isSelf
            && (
                (
                    SkipFollowers
                    && typeId is Constants.Actors.KelvinTypeId or Constants.Actors.VirginiaTypeId
                ) || (OnlyInSameAreaAsActor && graphMask != Actor.GraphMask)
            )
        )
        {
            return false;
        }

        return (selection ?? ActorSelection) switch
        {
            3 => true,
            2 => typeId == Actor.TypeId,
            1 => HasFamily && familyId == Actor.FamilyId,
            _ => isSelf,
        };
    }
}

public class ModifyOptions
{
    private float? _originalEnergy;
    private float? _originalHealth;

    public float ActorEnergy
    {
        get;
        set
        {
            UpdateEnergy = ShouldUpdate(ref _originalEnergy, value);
            field = value;
        }
    }

    public float ActorHealth
    {
        get;
        set
        {
            UpdateHealth = ShouldUpdate(ref _originalHealth, value);
            field = value;
        }
    }

    private static bool ShouldUpdate(ref float? original, float value)
    {
        // Written Only While Off The Loaded Value
        original ??= value;
        return Math.Abs(original.Value - value) >= 0.01;
    }

    public bool RemoveSpawner { get; set; }

    public ActorType? ReplaceType { get; set; }

    public string TeleportMode { get; set; } = "";

    public bool UpdateEnergy { get; private set; }

    public bool UpdateHealth { get; private set; }

    public bool UpdateInfluences { get; set; }

    public bool HasChanges =>
        UpdateEnergy
        || UpdateHealth
        || UpdateInfluences
        || RemoveSpawner
        || ReplaceType != null
        || TeleportMode != "";
}
