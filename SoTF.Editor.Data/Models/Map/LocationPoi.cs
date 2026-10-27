using SoTF.Editor.Data.Resources;

namespace SoTF.Editor.Data.Models.Map;

public class LocationPoi(Position position) : BasePoi(position)
{
    public override string Icon => BaseIcon("crosshair.svg");

    public override string Title => AppText.Get("Location");
}
