using System.Diagnostics;
using NLog;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.ViewModels;

namespace SoTF.Editor.Services.Integration;

public static class Updater
{
    // Releases Always Carry One Exe Named After Their Tag
    private const string ExecutablePrefix = "SoTF.Editor_v";
    private const string AssetUrlFormat =
        AppInfo.RepositoryUrl + "/releases/download/{0}/SoTF.Editor_{0}.exe";
    private const string AfterUpdateArgument = "--after-update";

    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    private static readonly HttpClient s_httpClient = new() { Timeout = TimeSpan.FromMinutes(5) };
    private static readonly TimeSpan s_previousExitWait = TimeSpan.FromSeconds(15);

    public static bool CanInstall =>
        Path.GetFileName(Environment.ProcessPath)
            ?.StartsWith(ExecutablePrefix, StringComparison.OrdinalIgnoreCase) == true;

    public static Task<string> DownloadAsync(ReleaseInfo release)
    {
        // Off The Window's Thread, So The Window Never Freezes
        return Task.Run(() => DownloadOnPoolAsync(release));
    }

    private static async Task<string> DownloadOnPoolAsync(ReleaseInfo release)
    {
        var started = Stopwatch.StartNew();
        var targetPath = Path.Combine(
            Path.GetDirectoryName(Environment.ProcessPath)!,
            $"SoTF.Editor_{release.Tag}.exe"
        );
        var partialPath = targetPath + ".download";

        try
        {
            using var response = await s_httpClient.GetAsync(
                string.Format(AssetUrlFormat, release.Tag),
                HttpCompletionOption.ResponseHeadersRead
            );
            response.EnsureSuccessStatusCode();
            await using (
                var file = new FileStream(
                    partialPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    FileOptions.Asynchronous
                )
            )
            {
                await response.Content.CopyToAsync(file);
            }

            // Only A Complete Download Gets The Real Name
            File.Move(partialPath, targetPath, overwrite: true);
            s_logger.Info(
                $"Downloaded Update {release.Tag} In {started.Elapsed.TotalSeconds:0.0} s To {targetPath}"
            );
            return targetPath;
        }
        catch
        {
            File.Delete(partialPath);
            throw;
        }
    }

    public static void Launch(string executablePath)
    {
        s_logger.Info($"Started Update {executablePath}");
        Process.Start(
            new ProcessStartInfo(executablePath, $"{AfterUpdateArgument} {Environment.ProcessId}")
            {
                UseShellExecute = false,
            }
        );
    }

    public static void FinishPreviousVersion(string[] arguments)
    {
        var index = Array.IndexOf(arguments, AfterUpdateArgument);
        if (
            index < 0
            || index + 1 >= arguments.Length
            || !int.TryParse(arguments[index + 1], out var processId)
        )
        {
            return;
        }

        try
        {
            using var previous = Process.GetProcessById(processId);
            previous.WaitForExit(s_previousExitWait);
        }
        catch (ArgumentException)
        {
            // Already Gone
        }

        var oldPaths = Directory
            .EnumerateFiles(
                Path.GetDirectoryName(Environment.ProcessPath)!,
                $"{ExecutablePrefix}*.exe"
            )
            .Where(path =>
                !string.Equals(path, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase)
            );
        foreach (var oldPath in oldPaths)
        {
            try
            {
                File.Delete(oldPath);
            }
            catch (Exception exception)
                when (exception is IOException or UnauthorizedAccessException)
            {
                // A Locked Old Exe Is Left For Next Time
            }
        }
    }
}
