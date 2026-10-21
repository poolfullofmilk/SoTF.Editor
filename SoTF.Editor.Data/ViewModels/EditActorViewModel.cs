using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models.Actors;
using SoTF.Editor.Data.Models.SaveData.Actor;
using SoTF.Editor.Data.Resources;

namespace SoTF.Editor.Data.ViewModels;

public class EditActorViewModel
{
    public EditActorViewModel(Actor actor, List<ActorType> allActorTypes)
    {
        Actor = actor;
        AllActorTypes = allActorTypes;
        ModifyOptions.ReplaceType = allActorTypes.First();
        ModifyOptions.ActorEnergy = Actor.Stats?.GetValueOrDefault("Energy", 100f) ?? 100f;
        ModifyOptions.UpdateEnergy = !Actor.Stats?.ContainsKey("Energy") ?? true;
        ModifyOptions.ActorHealth = Actor.Stats?.GetValueOrDefault("Health", 100f) ?? 100f;
        ModifyOptions.UpdateHealth = !Actor.Stats?.ContainsKey("Health") ?? true;

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

        switch (actor.TypeId)
        {
            case Constants.Actors.KelvinTypeId:
                SkipKelvin = false;
                break;
            case Constants.Actors.VirginiaTypeId:
                SkipVirginia = false;
                break;
        }
    }

    public short? ActorSelection { get; set; } = 0;

    public ActorModificationMode ModificationMode { get; set; } = ActorModificationMode.Modify;

    public bool OnlyInSameAreaAsActor { get; set; } = true;

    public bool SkipKelvin { get; set; } = true;

    public bool SkipVirginia { get; set; } = true;

    public List<Influence> Influences { get; }

    public ModifyOptions ModifyOptions { get; } = new();

    public static IEnumerable<KeyValuePair<short, string>> AllActorSelections =>
        new Dictionary<short, string>
        {
            { 0, AppText.Get("ThisActor") },
            { 1, AppText.Get("AllActorsOfFamily") },
            { 2, AppText.Get("AllActorsOfType") },
            { 3, AppText.Get("AllActors") },
        };

    public Actor Actor { get; }

    public List<ActorType> AllActorTypes { get; }
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
            // Back At The Loaded Value Means No Update
            _originalEnergy ??= value;
            if (Math.Abs(_originalEnergy.Value - value) < 0.01)
            {
                UpdateEnergy = false;
            }
            else if (Math.Abs(field - value) > 0.01)
            {
                UpdateEnergy = true;
            }

            field = value;
        }
    }

    public float ActorHealth
    {
        get;
        set
        {
            _originalHealth ??= value;
            if (Math.Abs(_originalHealth.Value - value) < 0.01)
            {
                UpdateHealth = false;
            }
            else if (Math.Abs(field - value) > 0.01)
            {
                UpdateHealth = true;
            }

            field = value;
        }
    }

    public bool RemoveSpawner { get; set; }

    public ActorType? ReplaceType { get; set; }

    public string TeleportMode { get; set; } = "";

    public bool UpdateEnergy { get; set; }

    public bool UpdateHealth { get; set; }

    public bool UpdateInfluences { get; set; }
}
