namespace SoTF.Editor.Data.Models.Map;

public class StructurePoi(ScrewStructureWrapper screwStructureWrapper)
    : BasePoi(screwStructureWrapper.Position!)
{
    public ScrewStructureWrapper ScrewStructureWrapper { get; } = screwStructureWrapper;

    public override string Title => ScrewStructureWrapper.Name;

    public override string Icon =>
        ScrewStructureWrapper.ScrewStructure?.Icon is { } icon
            ? $"images/structures/{icon}"
            : DefaultIcon;

    public override int IconSize => 24;

    protected override bool ShouldFilter(MapFilter mapFilter)
    {
        return mapFilter.RequirementsFilter == MapFilter.RequirementsFilterType.InaccessibleOnly
            || base.ShouldFilter(mapFilter);
    }
}
