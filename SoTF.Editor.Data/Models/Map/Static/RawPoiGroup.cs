using SoTF.Editor.Data.Enums;

namespace SoTF.Editor.Data.Models.Map.Static;

public record RawPoiGroup(
    string? Icon,
    List<RawPoi> Pois,
    PoiGroupType Type = PoiGroupType.Generic
);
