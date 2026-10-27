using System.Diagnostics;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

    // Checks Read The Redirect Itself, Downloads Follow It
    private static readonly HttpClient s_checkClient = new(
        new HttpClientHandler { AllowAutoRedirect = false }
    )
    {
        Timeout = TimeSpan.FromSeconds(10),
    };
    private static readonly HttpClient s_downloadClient = new()
    {
        Timeout = TimeSpan.FromMinutes(5),
    };
    private static readonly TimeSpan s_previousExitWait = TimeSpan.FromSeconds(15);

    public static async Task<ReleaseInfo?> CheckAsync()
    {
        try
        {
            // The Latest Release Url Redirects To The Tagged Release
            using var response = await s_checkClient.GetAsync(
                AppInfo.LatestReleaseUrl,
                HttpCompletionOption.ResponseHeadersRead
            );
            var location = response.Headers.Location?.ToString() ?? string.Empty;
            var tag = location[(location.LastIndexOf('/') + 1)..];

            return Version.TryParse(tag.TrimStart('v', 'V'), out var latest)
                ? new ReleaseInfo(tag, latest)
                : null;
        }
        catch (Exception exception)
            when (exception is HttpRequestException or TaskCanceledException)
        {
            // Unreachable Means Unknown, Never Up To Date
            return null;
        }
    }

    public static async Task<List<string>> NotesAsync(string tag)
    {
        try
        {
            // A Build Without Its Commit Reads The Release Body
            if (AppInfo.Commit is not { } commit)
            {
                var body = (await GetJsonAsync(AppInfo.ReleaseApiUrl + tag))["body"];
                return NotesOf(body?.ToString().Split('\n'));
            }

            // Ponytail: GitHub Compares 250 Commits At Most, Page Beyond That
            var comparison = await GetJsonAsync($"{AppInfo.CompareApiUrl}{commit}...{tag}");

            // Every Commit Since This Build, Newest First Like The Body
            return NotesOf(
                comparison["commits"]
                    ?.Reverse()
                    .Select(entry => entry["commit"]?["message"]?.ToString().Split('\n')[0])
            );
        }
        catch (Exception exception)
            when (exception is HttpRequestException or TaskCanceledException or JsonReaderException)
        {
            // Notes Are Extra, The Update Works Without Them
            return [];
        }
    }

    private static List<string> NotesOf(IEnumerable<string?>? lines) =>
        [
            .. (lines ?? [])
                .OfType<string>()
                .Select(line => line.Trim().TrimStart('-', '*').Trim())
                .Where(line => line.Length > 0)
                .Distinct(),
        ];

    private static async Task<JObject> GetJsonAsync(string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd(AppInfo.DisplayName.Replace(' ', '.'));
        using var response = await s_checkClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return JObject.Parse(await response.Content.ReadAsStringAsync());
    }

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
        var expectedDigest = await ExpectedDigestAsync(release.Tag);

        try
        {
            using var response = await s_downloadClient.GetAsync(
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

            // A Download That Differs From GitHub's Checksum Never Runs
            await using (var downloaded = File.OpenRead(partialPath))
            {
                var digest = Convert.ToHexStringLower(await SHA256.HashDataAsync(downloaded));
                if (digest != expectedDigest)
                {
                    throw new InvalidDataException($"Checksum Mismatch For {release.Tag}");
                }
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

    private static async Task<string> ExpectedDigestAsync(string tag)
    {
        // GitHub Hashes Every Release Asset, Its API Lists The Hash
        string? digest;
        try
        {
            digest = (await GetJsonAsync(AppInfo.ReleaseApiUrl + tag))["assets"]
                ?.FirstOrDefault(asset => asset["name"]?.ToString() == $"SoTF.Editor_{tag}.exe")
                ?["digest"]?.ToString();
        }
        catch (JsonReaderException exception)
        {
            throw new InvalidDataException($"Unreadable Release Details For {tag}", exception);
        }

        const string prefix = "sha256:";
        return digest?.StartsWith(prefix, StringComparison.Ordinal) == true
            ? digest[prefix.Length..].ToLowerInvariant()
            : throw new InvalidDataException($"Release {tag} Lists No Checksum");
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
