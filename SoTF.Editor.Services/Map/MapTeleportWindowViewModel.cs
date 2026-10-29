using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.Actors;
using SoTF.Editor.Data.Models.Map;
using SoTF.Editor.Data.Resources;
using SoTF.Editor.Services.Pages;

namespace SoTF.Editor.Services.Map;

public class MapTeleportWindowViewModel(
    BasePoi destination,
    TeleportTarget teleportTarget,
    AreaMaskManager areaMaskManager,
    FollowerPageViewModel followers
)
{
    // Every Copy Of A Follower Goes, Spawned Ones Too
    private readonly List<FollowerState> _copies = teleportTarget switch
    {
        TeleportTarget.Kelvin => followers.CopiesOf(Constants.Actors.KelvinTypeId),
        TeleportTarget.Virginia => followers.CopiesOf(Constants.Actors.VirginiaTypeId),
        _ => [],
    };

    public Landing Landing { get; } =
        new(destination, destination.TeleportationOffset, areaMaskManager);

    public string Target =>
        _copies.Count > 1
            ? AppText.Format(
                "TeleportCopies",
                AppText.Get(TextKeys.TeleportTarget(teleportTarget)),
                _copies.Count
            )
            : AppText.Get(TextKeys.TeleportTarget(teleportTarget));
    public string Destination => destination.Title;

    public void DoTeleport(PlayerState playerState)
    {
        if (teleportTarget == TeleportTarget.Player)
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
