namespace SoTF.Editor.Data.ViewModels.Progress;

public sealed record PlayerProgress(
    IReadOnlyList<CollectibleGroup> Collectibles,
    int Days,
    int? TreesCut,
    IReadOnlyList<Tally>? Kills,
    IReadOnlyList<Tally> Crafted,
    IReadOnlyList<Tally> Used
);
