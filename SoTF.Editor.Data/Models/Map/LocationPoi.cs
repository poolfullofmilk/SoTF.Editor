using SoTF.Editor.Data.Resources;

namespace SoTF.Editor.Data.Models.Map;

public sealed class LocationPoi(Position position) : BasePoi(position)
{
    public const string IconFile = "pin.svg";

    public override string Icon => BaseIcon(IconFile);

    public override string Title => AppText.Get("Location");
}
