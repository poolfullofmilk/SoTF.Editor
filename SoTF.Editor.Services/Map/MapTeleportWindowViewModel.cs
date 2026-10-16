using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.Map;
using SoTF.Editor.Data.Resources;
using SoTF.Editor.Services.Pages;

namespace SoTF.Editor.Services.Map;

public class MapTeleportWindowViewModel
{
    private readonly BasePoi _destination;

    private readonly TeleportTarget _teleportTarget;

    public MapTeleportWindowViewModel(
        BasePoi destination,
        TeleportTarget teleportTarget,
        AreaMaskManager areaMaskManager
    )
    {
        _destination = destination;
        _teleportTarget = teleportTarget;
        X = destination.Position?.X ?? destination.X;
        Y = destination.Position?.Y ?? 0;
        Z = destination.Position?.Z ?? destination.Z;

        destination.GetTeleportationOffset(out var xOffset, out var yOffset, out var zOffset);
        XOffset = xOffset;
        YOffset = yOffset;
        ZOffset = zOffset;

        Areas = [.. areaMaskManager.GetAllAreas().OrderBy(area => area.Name)];
        SelectedArea = destination.Position?.Area ?? AreaMaskManager.Surface;
    }

    public Area SelectedArea { get; set; }

    public float XOffset { get; set; }

    public float YOffset { get; set; }

    public float ZOffset { get; set; }

    public List<Area> Areas { get; }

    public float X { get; }
    public float Y { get; }
    public float Z { get; }

    public string Target => AppText.Get(TextKeys.TeleportTarget(_teleportTarget));
    public string Destination => _destination.Title;

    public void DoTeleport(PlayerState playerState, FollowerPageViewModel followers)
    {
        var newPosition = new Position(X + XOffset, Y + YOffset, Z + ZOffset)
        {
            Area = SelectedArea,
        };

        switch (_teleportTarget)
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
