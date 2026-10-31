namespace SoTF.Editor.Data.ViewModels;

public sealed record SavegameBackup(string Path, DateTime TakenAt, long Size, string? Label = null)
{
    public bool IsPinned => Label is not null;
}
