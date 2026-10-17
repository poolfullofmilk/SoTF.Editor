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
}
