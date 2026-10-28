using Microsoft.AspNetCore.Components;
using MudBlazor;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.ViewModels;
using SoTF.Editor.Desktop.Components.Layout;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Desktop.Components.Shared.Common;

public partial class DifferenceList
{
    [Parameter]
    public required Dictionary<string, List<Change>> Changes { get; set; }

    // Pages In Menu Order, Other Files Last
    private IEnumerable<KeyValuePair<string, List<Change>>> Ordered =>
        Changes.OrderBy(pair =>
            PageOf(pair.Key) is { } link ? MainLayout.Pages.ToList().IndexOf(link) : int.MaxValue
        );

    public static Dictionary<string, List<Change>> Describe(
        string nowPath,
        string thenPath,
        GameData gameData
    )
    {
        return BackupDiff.Describe(
            nowPath,
            thenPath,
            gameData,
            key => AppText.Get(Pages.Game.SetupLabelKey(key), BackupDiff.Words(key))
        );
    }

    private static MainLayout.PageLink? PageOf(string href)
    {
        return MainLayout.Pages.FirstOrDefault(link => link.Href == href);
    }

    private static string LabelOf(string href)
    {
        return PageOf(href) is { } link ? AppText.Get(link.LabelKey) : AppText.Get(href, href);
    }

    // Restores And Other Files Are No Page, Own Icons
    private static string? IconOf(string href)
    {
        return href switch
        {
            BackupManager.RestoreKey => Icons.Material.Rounded.Restore,
            BackupDiff.FilesKey => Icons.Material.Rounded.Description,
            _ => PageOf(href)?.Icon,
        };
    }
}
