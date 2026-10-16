namespace SoTF.Editor.Data.Models.Map;

public interface IAreaFilter
{
    public string Name { get; }
    public bool ShouldInclude(IPoi poi);
}
