using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models.Actors;

namespace SoTF.Editor.Data.Models.Map;

public class ActorPoi : BasePoi
{
    public ActorPoi(Actor actor)
        : base(actor.Position!)
    {
        Actor = actor;
        if (actor.TypeId is not (Constants.Actors.KelvinTypeId or Constants.Actors.VirginiaTypeId))
        {
            return;
        }

        IsFollower = true;
        Enabled = true;
    }

    public Actor Actor { get; }

    public bool IsFollower { get; }

    public override string Title => Actor.PrintableTitle;

    public override string Icon => Actor.ActorType?.IconPath ?? DefaultIcon;

    public override int IconZIndex => IsFollower ? 50 : base.IconZIndex;

    protected override bool ShouldFilter(MapFilter mapFilter)
    {
        return mapFilter.RequirementsFilter == MapFilter.RequirementsFilterType.InaccessibleOnly
            || base.ShouldFilter(mapFilter);
    }
}
