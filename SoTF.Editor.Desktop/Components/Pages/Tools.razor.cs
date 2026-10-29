using Microsoft.AspNetCore.Components;
using MudBlazor;
using SoTF.Editor.Desktop.Components.Shared.Common;
using SoTF.Editor.Desktop.Components.Shared.Dialogs;
using SoTF.Editor.Services.Pages;

namespace SoTF.Editor.Desktop.Components.Pages;

public partial class Tools : EditorPageBase
{
    // Each Tool With Its Text, Icon And Action
    private static readonly ToolSection[] s_sections =
    [
        new(
            "ToolsWorld",
            [
                DialogTool<RegrowTreesDialog>("RegrowTrees", Icons.Material.Rounded.Forest),
                ConfirmedTool(
                    "EternalFires",
                    Icons.Material.Rounded.LocalFireDepartment,
                    page => page.MainViewModel.EternalFires()
                ),
                ConfirmedTool(
                    "ResetFires",
                    Icons.Material.Rounded.Fireplace,
                    page => page.MainViewModel.ResetFires()
                ),
                ConfirmedTool(
                    "ResetStructureDamage",
                    Icons.Material.Rounded.HomeRepairService,
                    page => page.MainViewModel.ResetStructureDamage()
                ),
                ConfirmedTool(
                    "ResetTraps",
                    Icons.Material.Rounded.PestControl,
                    page => page.MainViewModel.ResetTraps()
                ),
                ConfirmedTool(
                    "ResetContainers",
                    Icons.Material.Rounded.Inventory2,
                    page => page.GameStatePage.ResetContainers()
                ),
                DialogTool<CannibalAngerDialog>(
                    "CannibalAnger",
                    Icons.Material.Rounded.SentimentVeryDissatisfied
                ),
                ConfirmedTool(
                    "SetToEndgame",
                    Icons.Material.Rounded.Flag,
                    page => page.SetToEndgame()
                ),
                DialogTool<WorldItemClonerDialog>(
                    "WorldItemCloner",
                    Icons.Material.Rounded.ContentCopy
                ),
            ]
        ),
        new(
            "ToolsPlayer",
            [
                DialogTool<ConsumedItemsDialog>(
                    "ConsumedItems",
                    Icons.Material.Rounded.Restaurant
                ) with
                {
                    NeedsWorld = false,
                },
                DialogTool<ItemPlaterDialog>("ItemPlater", Icons.Material.Rounded.AutoAwesome) with
                {
                    NeedsWorld = false,
                    SavesAtOnce = true,
                },
                DialogTool<UnlocksDialog>("Unlocks", Icons.Material.Rounded.LockOpen) with
                {
                    NeedsWorld = false,
                    SavesAtOnce = true,
                },
            ]
        ),
    ];

    [Inject]
    public required GameStatePageViewModel GameStatePage { get; set; }

    [Inject]
    public required ISnackbar Snackbar { get; set; }

    // A Client Save Holds Only The Player, Not The World
    private bool HasWorld => MainViewModel.SelectedSavegame?.IsMultiPlayerClient() == false;

    private bool IsAvailable(Tool tool)
    {
        return HasWorld || !tool.NeedsWorld;
    }

    private string CardClass(Tool tool)
    {
        return "rounded-panel pa-4 mud-height-full"
            + (IsAvailable(tool) ? " hover-card" : " tool-unavailable")
            + (MainViewModel.IsToolChanged(tool.TitleKey) ? " card-changed" : "");
    }

    // Only The Exceptions Wear A Chip
    private string? ChipKey(Tool tool)
    {
        if (!IsAvailable(tool))
        {
            return "HostSaveOnly";
        }

        return tool.SavesAtOnce ? "SavesAtOnce" : null;
    }

    private Color ChipColor(Tool tool)
    {
        return IsAvailable(tool) && tool.SavesAtOnce ? Color.Warning : Color.Default;
    }

    // A Tool Setting A Level Shows Where It Stands Now
    private string? ValueOf(Tool tool)
    {
        return
            tool.TitleKey == "CannibalAnger"
            && IsAvailable(tool)
            && MainViewModel.ReadCannibalMood() is { } mood
            ? AppText.Format("CannibalAngerNow", mood.Average)
            : null;
    }

    private async Task RunAsync(Tool tool)
    {
        if (!IsAvailable(tool))
        {
            return;
        }

        var stateBefore = MainViewModel.ToolState();
        await tool.Run(this);
        MainViewModel.FinishTool(tool.TitleKey, stateBefore);
    }

    private async Task ConfirmAndRunAsync(string titleKey, Func<Tools, string> action)
    {
        if (!await ConfirmAsync(titleKey, titleKey + "Text"))
        {
            return;
        }

        var message = action(this);
        if (!string.IsNullOrEmpty(message))
        {
            Snackbar.Add(message, Severity.Info);
        }
    }

    private string SetToEndgame()
    {
        GameStatePage.SetToEndgame();
        return AppText.Get("ChangesAdded");
    }

    private async Task ShowDialogAsync<TDialog>()
        where TDialog : IComponent
    {
        var dialog = await DialogService.ShowAsync<TDialog>(
            (string?)null,
            new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true }
        );
        await dialog.Result;
    }

    private static Tool DialogTool<TDialog>(string titleKey, string icon)
        where TDialog : IComponent
    {
        return new Tool(titleKey, icon, page => page.ShowDialogAsync<TDialog>());
    }

    private static Tool ConfirmedTool(string titleKey, string icon, Func<Tools, string> action)
    {
        return new Tool(titleKey, icon, page => page.ConfirmAndRunAsync(titleKey, action));
    }

    private sealed record Tool(string TitleKey, string Icon, Func<Tools, Task> Run)
    {
        public string TextKey => TitleKey + "Text";

        public bool NeedsWorld { get; init; } = true;

        public bool SavesAtOnce { get; init; }
    }

    private sealed record ToolSection(string TitleKey, Tool[] Tools);
}
