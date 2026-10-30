using Microsoft.AspNetCore.Components;
using MudBlazor;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.ViewModels.Progress;
using SoTF.Editor.Desktop.Components.Shared.Common;
using SoTF.Editor.Services.Pages;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Desktop.Components.Pages;

public partial class Progress : EditorPageBase
{
    [Inject]
    public required GameData GameData { get; set; }

    // Read Only, Nothing Here Edits The Savegame
    protected override bool EditsSavegame => false;

    // Read Again Only When The Savegame Or Its Revision Moves
    private (Savegame Savegame, int Revision, PlayerProgress Progress)? _read;

    private PlayerProgress PlayerProgress
    {
        get
        {
            var savegame = MainViewModel.SelectedSavegame!;
            if (
                _read is not { } read
                || read.Savegame != savegame
                || read.Revision != MainViewModel.Revision
            )
            {
                read = (savegame, MainViewModel.Revision, ProgressReader.Read(savegame, GameData));
                _read = read;
            }

            return read.Progress;
        }
    }

    private IEnumerable<(string LabelKey, string Value)> Totals =>
        [
            ("DaysSurvived", PlayerProgress.Days.ToString("N0")),
            ("Kills", PlayerProgress.Kills?.Sum(tally => tally.Count).ToString("N0") ?? "-"),
            ("TreesCut", PlayerProgress.TreesCut?.ToString("N0") ?? "-"),
            ("ItemsCrafted", PlayerProgress.Crafted.Sum(tally => tally.Count).ToString("N0")),
        ];

    private IEnumerable<(string TitleKey, IReadOnlyList<Tally>? Tallies)> Lists =>
        [
            ("Kills", PlayerProgress.Kills),
            ("ItemsCrafted", PlayerProgress.Crafted),
            ("ItemsUsed", PlayerProgress.Used),
        ];

    private static string IconOf(CollectibleGroup group)
    {
        return group.TitleKey switch
        {
            "Bunkers" => Icons.Material.Rounded.Warehouse,
            "ActionCameraVideos" => Icons.Material.Rounded.Videocam,
            "MissingSoldiers" => Icons.Material.Rounded.PersonSearch,
            _ => Icons.Material.Rounded.MenuBook,
        };
    }

    private static string CardClass(Collectible collectible)
    {
        // What Is Still Out There Stays Grey
        return "rounded-panel pa-4 flex-grow-1 d-flex" + (collectible.Found ? "" : " not-held");
    }
}
