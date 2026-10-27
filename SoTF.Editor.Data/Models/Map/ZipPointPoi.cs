using SoTF.Editor.Data.Resources;

namespace SoTF.Editor.Data.Models.Map;

public class ZipPointPoi(Position position, ZiplinePoi parent) : BasePoi(position)
{
    public const string IconFile = "pole.svg";

    public ZiplinePoi Parent { get; } = parent;

    public override string Icon => BaseIcon(IconFile);

    public override int IconZIndex => -1;

    public override string Title => AppText.Get("ZiplineAnchor");
}
