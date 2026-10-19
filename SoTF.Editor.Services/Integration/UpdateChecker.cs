using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.ViewModels;

namespace SoTF.Editor.Services.Integration;

public static class UpdateChecker
{
    private static readonly HttpClient s_httpClient = new(
        new HttpClientHandler { AllowAutoRedirect = false }
    )
    {
        Timeout = TimeSpan.FromSeconds(10),
    };

    public static async Task<ReleaseInfo?> CheckAsync()
    {
        try
        {
            // The Latest Release Url Redirects To The Tagged Release
            using var response = await s_httpClient.GetAsync(
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
            // The Release Body Lists Commit Titles Since The Previous Tag
            using var request = new HttpRequestMessage(HttpMethod.Get, AppInfo.ReleaseApiUrl + tag);
            request.Headers.UserAgent.ParseAdd(AppInfo.DisplayName.Replace(' ', '.'));
            using var response = await s_httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            var body = JObject.Parse(await response.Content.ReadAsStringAsync())["body"];
            return
            [
                .. (body?.ToString() ?? "")
                    .Split('\n')
                    .Select(line => line.Trim().TrimStart('-', '*').Trim())
                    .Where(line => line.Length > 0),
            ];
        }
        catch (Exception exception)
            when (exception is HttpRequestException or TaskCanceledException or JsonReaderException)
        {
            // Notes Are Extra, The Update Works Without Them
            return [];
        }
    }
}
