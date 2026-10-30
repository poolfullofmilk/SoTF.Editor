using Microsoft.AspNetCore.Components;
using MudBlazor;
using NLog;
using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Resources;
using SoTF.Editor.Services.Savegames;
using SoTF.Editor.Services.Tools;

namespace SoTF.Editor.Desktop.Components.Shared.Dialogs;

public partial class CopyPlayerDialog
{
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    private static readonly PlayerPart[] s_parts = Enum.GetValues<PlayerPart>();

    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }

    [Inject]
    public required MainViewModel MainViewModel { get; set; }

    [Inject]
    public required IDialogService DialogService { get; set; }

    [Inject]
    public required ISnackbar Snackbar { get; set; }

    // Every Part At First, The Chips Take Some Away
    private IReadOnlyCollection<PlayerPart> _parts = s_parts;
    private List<Savegame>? _savegames;
    private Savegame? _target;
    private readonly bool _gameRunning = SavegameManager.IsGameRunning();

    protected override async Task OnInitializedAsync()
    {
        // Every Other Savegame, Read Off The Window's Thread
        var openPath = MainViewModel.SelectedSavegame?.FullPath;
        _savegames = await Task.Run(() =>
        {
            var others = SavegameManager
                .GetSavegames()
                .Where(savegame =>
                    !string.Equals(savegame.FullPath, openPath, StringComparison.OrdinalIgnoreCase)
                )
                .ToList();
            others.ForEach(savegame => _ = savegame.Summary);
            return others;
        });
    }

    private static string LabelKeyOf(PlayerPart part)
    {
        return part switch
        {
            PlayerPart.Stats => "Stats",
            PlayerPart.Inventory => "Inventory",
            PlayerPart.Armor => "Armor",
            _ => "Outfit",
        };
    }

    private string RowClass(Savegame savegame)
    {
        return "list-row px-4 py-2" + (savegame == _target ? " list-row-selected" : "");
    }

    private static string DetailOf(Savegame savegame)
    {
        // Its Group, Then The Day It Reached
        return Formatter.JoinParts(
            AppText.Get(
                savegame.IsMultiPlayerClient() ? "JoinedMultiplayer"
                : savegame.IsMultiPlayer() ? "HostedMultiplayer"
                : "Singleplayer"
            ),
            savegame.Summary is { } summary ? AppText.Format("SavegameDay", summary.Days) : null
        );
    }

    private async Task CopyAsync()
    {
        if (_target is not { } target || MainViewModel.SelectedSavegame is not { } source)
        {
            return;
        }

        if (
            !await DialogService.ConfirmAsync(
                AppText.Get("CopyPlayer"),
                AppText.Format("CopyPlayerConfirm", target.Name)
            )
        )
        {
            return;
        }

        try
        {
            var copied = PlayerCopier.Copy(source.FullPath, target.FullPath, _parts, source.Name);
            Snackbar.Add(
                AppText.Format(copied.Count > 0 ? "PlayerCopied" : "NothingCopied", target.Name),
                copied.Count > 0 ? Severity.Success : Severity.Warning
            );
            MudDialog.Close();
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            s_logger.Error(exception, "Failed To Copy Player");
            Snackbar.Add(AppText.Get("UnableToCopyPlayer"), Severity.Error);
        }
    }
}
