namespace SoTF.Editor.Data.Models.Map.Static;

public sealed record RawItemPoi(
    string? Description,
    float X,
    float Y,
    int[]? Requirements,
    int[]? AltItemIds,
    Teleport Teleport
);
