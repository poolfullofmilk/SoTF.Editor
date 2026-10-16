using SoTF.Editor.Data.Resources;

namespace SoTF.Editor.Data.Models.Map;

public class PlayerPoi(Position position) : BasePoi(position)
{
    public const string IconFile = "player.svg";

    public override string Icon => BaseIcon(IconFile);

    public override int IconZIndex => 50;

    public override string Title => AppText.Get("Player");
}
