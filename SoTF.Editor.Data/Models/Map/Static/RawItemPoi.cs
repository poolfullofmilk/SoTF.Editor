namespace SoTF.Editor.Data.Models.Map.Static;

public record RawItemPoi(
    string? Description,
    float X,
    float Y,
    int[]? Requirements,
    int[]? AltItemIds,
    Teleport Teleport
);
