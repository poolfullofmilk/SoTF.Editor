using SoTF.Editor.Data.Enums;

namespace SoTF.Editor.Data.Models.Map;

public interface IPoiGrouper
{
    public string Title { get; }
    public string BaseTitle { get; }
    public bool Enabled { get; }
    public PoiGroupType GroupType { get; }
    public void SetEnabledNoRefresh(bool value);
}
