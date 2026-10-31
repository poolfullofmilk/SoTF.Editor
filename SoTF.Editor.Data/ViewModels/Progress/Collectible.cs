using SoTF.Editor.Data.Models;

namespace SoTF.Editor.Data.ViewModels.Progress;

public sealed record Collectible(
    string Name,
    bool Found,
    string? ImageUrl = null,
    Position? Position = null
);
