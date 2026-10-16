namespace SoTF.Editor.Data.Models.Map.Static;

public record RawItemPoiCollection(
    Dictionary<string, string> OverridingTypeIcons,
    HashSet<string> DefaultEnabledGroups,
    Dictionary<int, RawItemPoiGroup> Items
);
