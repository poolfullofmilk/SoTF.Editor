using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Resources;

namespace SoTF.Editor.Data.Models.Actors;

public record ActorType(
    int Id,
    string Classification,
    string? Gender = null,
    string? Image = null,
    string? Icon = null
)
{
    public string IconPath =>
        Icon != null ? $"images/actors/icons/{Icon}" : "images/icons/question-mark.svg";

    public string Name => Id == -1 ? "???" : GameNames.Get(TextKeys.ActorType(Id));

    public string PrintableClassification => AppText.Get(TextKeys.Classification(Classification));

    public string PrintableGender => Gender == null ? "-" : AppText.Get(TextKeys.Gender(Gender));

    public bool IsCannibal => Classification is "cannibal" or "muddyCannibal";

    public virtual bool Equals(ActorType? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Id == other.Id;
    }

    public override int GetHashCode()
    {
        return Id;
    }

    public bool IsFollower()
    {
        return Id is Constants.Actors.KelvinTypeId or Constants.Actors.VirginiaTypeId;
    }
}

public record EmptyActorType() : ActorType(-1, "")
{
    public static new string Name => "";
}
