namespace SoTF.Editor.Data.Models.Map;

public interface IPoiWithItems
{
    public IEnumerable<ItemInInventoryWrapper>? Items { get; }
    public bool HasAnyItems { get; }
}
