namespace SoTF.Editor.Data.ViewModels.Progress;

public sealed record CollectibleGroup(string TitleKey, IReadOnlyList<Collectible> Items)
{
    public int FoundCount => Items.Count(item => item.Found);
}
