using SoTF.Editor.Data.Resources;

namespace SoTF.Editor.Data.Models.Map;

public class ZipPointPoi(Position position, ZiplinePoi parent) : BasePoi(position)
{
    public const string IconFile = "pole.svg";

    public bool IsMoveRequested { get; set; }

    public bool IsZiplineCreationRequested { get; set; }

    public ZiplinePoi Parent { get; } = parent;

    public override string Icon => BaseIcon(IconFile);

    public override int IconZIndex => -1;

    public override string Title => AppText.Get("ZiplineAnchor");

    protected override bool ShouldFilter(MapFilter mapFilter)
    {
        return mapFilter.RequirementsFilter == MapFilter.RequirementsFilterType.InaccessibleOnly
            || base.ShouldFilter(mapFilter);
    }
}
