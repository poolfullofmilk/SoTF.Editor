using SoTF.Editor.Data.Enums;

namespace SoTF.Editor.Data.Models.Map.Static;

public record RawPoiGroup(
    string? Icon,
    List<RawPoi> Pois,
    bool AlwaysEnabled = false,
    PoiGroupType Type = PoiGroupType.Generic
);
