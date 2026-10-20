using System.Reflection;

namespace SoTF.Editor.Data.Helpers;

public static class AppInfo
{
    public const string DisplayName = "SoTF Editor";
    public const string Author = "poolfullofmilk";
    public const string RepositoryUrl = "https://github.com/poolfullofmilk/SoTF.Editor";
    public const string LatestReleaseUrl = RepositoryUrl + "/releases/latest";
    public const string ReleaseApiUrl =
        "https://api.github.com/repos/" + Author + "/SoTF.Editor/releases/tags/";
    public const string NoticesUrl = RepositoryUrl + "/blob/master/THIRD-PARTY-NOTICES.txt";

    public static Version Version { get; } =
        Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0);

    // Patch Shows Only When It Is Not Zero, 2.7 And 2.7.1
    public static string VersionText { get; } = Version.ToString(Version.Build > 0 ? 3 : 2);

    // Debug Builds Never Touch The Installed App's Files
#if DEBUG
    public const string FolderName = "SoTF.Editor Debug";
#else
    public const string FolderName = "SoTF.Editor";
#endif

    public static readonly string DataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        FolderName
    );

    // Logs And Backups Sit Where People Look For Them
    public static readonly string DocumentsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        FolderName
    );
}
