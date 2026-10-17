using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.Map;
using SoTF.Editor.Data.Resources;
using SoTF.Editor.Services.Pages;

namespace SoTF.Editor.Services.Map;

public class MapTeleportWindowViewModel(
    BasePoi destination,
    TeleportTarget teleportTarget,
    AreaMaskManager areaMaskManager
)
{
    public Area SelectedArea { get; set; } = destination.Position?.Area ?? AreaMaskManager.Surface;

    public float XOffset { get; set; } = destination.TeleportationOffset;

    public float YOffset { get; set; } = destination.TeleportationOffset;

    public float ZOffset { get; set; } = destination.TeleportationOffset;

    public List<Area> Areas { get; } =
    [.. areaMaskManager.GetAllAreas().OrderBy(area => area.Name)];

    public float X { get; } = destination.Position?.X ?? destination.X;
    public float Y { get; } = destination.Position?.Y ?? 0;
    public float Z { get; } = destination.Position?.Z ?? destination.Z;

    public string Target => AppText.Get(TextKeys.TeleportTarget(teleportTarget));
    public string Destination => destination.Title;

    public void DoTeleport(PlayerState playerState, FollowerPageViewModel followers)
    {
        var newPosition = new Position(X + XOffset, Y + YOffset, Z + ZOffset)
        {
            Area = SelectedArea,
        };

        switch (teleportTarget)
        {
            case TeleportTarget.Kelvin:
                followers.KelvinState.Pos = newPosition;
                break;
            case TeleportTarget.Virginia:
                followers.VirginiaState.Pos = newPosition;
                break;
            default:
                playerState.Pos = newPosition;
                break;
        }
    }
}
