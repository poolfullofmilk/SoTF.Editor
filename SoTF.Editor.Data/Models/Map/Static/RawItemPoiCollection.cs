namespace SoTF.Editor.Data.Models.Map.Static;

public record RawItemPoiCollection(
    Dictionary<string, string> OverridingTypeIcons,
    Dictionary<int, RawItemPoiGroup> Items
);
