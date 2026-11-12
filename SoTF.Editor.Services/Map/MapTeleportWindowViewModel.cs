using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.Actors;
using SoTF.Editor.Data.Models.Map;
using SoTF.Editor.Data.Resources;
using SoTF.Editor.Services.Pages;

namespace SoTF.Editor.Services.Map;

public sealed class MapTeleportWindowViewModel
{
    private readonly BasePoi _destination;
    private readonly TeleportTarget _teleportTarget;

    // Every Copy Of A Follower Goes, Spawned Ones Too
    private readonly List<FollowerState> _copies;

    public MapTeleportWindowViewModel(
        BasePoi destination,
        TeleportTarget teleportTarget,
        AreaMaskManager areaMaskManager,
        FollowerPageViewModel followers
    )
    {
        _destination = destination;
        _teleportTarget = teleportTarget;
        _copies = teleportTarget switch
        {
            TeleportTarget.Kelvin => followers.CopiesOf(Constants.Actors.KelvinTypeId),
            TeleportTarget.Virginia => followers.CopiesOf(Constants.Actors.VirginiaTypeId),
            _ => [],
        };
        Landing = new Landing(destination, destination.LandingLift);

        // Actors Name Caves By Graph Mask, Players By Area
        AreaChoices =
            teleportTarget == TeleportTarget.Player && destination is ActorPoi
                ? areaMaskManager.CavesSharingGraphMask(Landing.Area)
                : [];
        IsAreaPicked = AreaChoices.Count == 0;
    }

    public Landing Landing { get; }

    public List<Area> AreaChoices { get; }

    public bool IsAreaPicked { get; private set; }

    public string Target =>
        _copies.Count > 1
            ? AppText.Format(
                "TeleportCopies",
                AppText.Get(TextKeys.TeleportTarget(_teleportTarget)),
                _copies.Count
            )
            : AppText.Get(TextKeys.TeleportTarget(_teleportTarget));
    public string Destination => _destination.Title;

    public void PickArea(Area area)
    {
        if (AreaChoices.Contains(area))
        {
            Landing.Area = area;
            IsAreaPicked = true;
        }
    }

    public void DoTeleport(PlayerState playerState)
    {
        // Two Caves Share The Mask, Pick One First
        if (!IsAreaPicked)
        {
            return;
        }

        if (_teleportTarget == TeleportTarget.Player)
        {
            playerState.Pos = Landing.At();
            return;
        }

        // Copies Land A Step Apart, Not Inside Each Other
        foreach (var (index, copy) in _copies.Index())
        {
            copy.Pos = Landing.At(index);
        }
    }
}
