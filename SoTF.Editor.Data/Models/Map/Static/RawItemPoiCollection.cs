namespace SoTF.Editor.Data.Models.Map.Static;

public sealed record RawItemPoiCollection(
    Dictionary<string, string> OverridingTypeIcons,
    Dictionary<int, RawItemPoiGroup> Items
);
