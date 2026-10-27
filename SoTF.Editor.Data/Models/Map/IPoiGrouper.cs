using SoTF.Editor.Data.Enums;

namespace SoTF.Editor.Data.Models.Map;

public interface IPoiGrouper
{
    public string Title { get; }
    public int Count { get; }
    public bool Enabled { get; set; }
    public PoiGroupType GroupType { get; }
}
