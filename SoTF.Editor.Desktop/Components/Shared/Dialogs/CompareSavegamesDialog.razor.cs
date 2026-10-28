using Microsoft.AspNetCore.Components;
using MudBlazor;
using NLog;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.ViewModels;
using SoTF.Editor.Desktop.Components.Shared.Common;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Desktop.Components.Shared.Dialogs;

public partial class CompareSavegamesDialog
{
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();

    [Inject]
    public required GameData GameData { get; set; }

    [Parameter]
    public required Savegame Then { get; set; }

    [Parameter]
    public required Savegame Now { get; set; }

    private Dictionary<string, List<Change>>? _differences;
    private bool _failed;

    private string? StatusText =>
        _failed ? AppText.Get("UnableToCompare")
        : _differences is null ? AppText.Get("Comparing")
        : _differences.Count == 0 ? AppText.Get("SavegamesSame")
        : null;

    public static Task ShowAsync(IDialogService dialogService, Savegame then, Savegame now)
    {
        return dialogService.ShowAsync<CompareSavegamesDialog>(
            null,
            new DialogParameters<CompareSavegamesDialog>
            {
                { dialog => dialog.Then, then },
                { dialog => dialog.Now, now },
            },
            new DialogOptions { MaxWidth = MaxWidth.ExtraSmall, FullWidth = true }
        );
    }

    protected override async Task OnInitializedAsync()
    {
        // Reading Both Saves Takes A Moment, Off The Window's Thread
        try
        {
            _differences = await Task.Run(() =>
                DifferenceList.Describe(Now.FullPath, Then.FullPath, GameData)
            );
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            s_logger.Warn(exception, "Failed To Compare Savegames");
            _failed = true;
        }
    }

    private static string NameOf(Savegame savegame)
    {
        return Formatter.JoinParts(savegame.Name, Formatter.FormatDateTime(savegame.LastSaveTime));
    }
}
