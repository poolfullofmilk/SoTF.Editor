namespace SoTF.Editor.Data.ViewModels;

public sealed record SavegameSummary(
    int Days,
    string? GameType,
    string? CrashSite,
    int StrengthLevel,
    int ItemCount
);
