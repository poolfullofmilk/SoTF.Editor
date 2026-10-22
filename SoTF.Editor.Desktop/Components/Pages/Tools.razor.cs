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
                new(
                    "RegrowTrees",
                    "RegrowTreesText",
                    Icons.Material.Rounded.Forest,
                    ToolKind.RegrowTrees
                ),
                new(
                    "EternalFires",
                    "EternalFiresText",
                    Icons.Material.Rounded.LocalFireDepartment,
                    ToolKind.EternalFires
                ),
                new(
                    "ResetFires",
                    "ResetFiresText",
                    Icons.Material.Rounded.Fireplace,
                    ToolKind.ResetFires
                ),
                new(
                    "ResetStructureDamage",
                    "ResetStructureDamageText",
                    Icons.Material.Rounded.HomeRepairService,
                    ToolKind.ResetStructureDamage
                ),
                new(
                    "ResetTraps",
                    "ResetTrapsText",
                    Icons.Material.Rounded.PestControl,
                    ToolKind.ResetTraps
                ),
                new(
                    "ResetContainers",
                    "ResetContainersText",
                    Icons.Material.Rounded.Inventory2,
                    ToolKind.ResetContainers
                ),
                new(
                    "CannibalAnger",
                    "CannibalAngerText",
                    Icons.Material.Rounded.SentimentVeryDissatisfied,
                    ToolKind.CannibalAnger
                ),
                new(
                    "SetToEndgame",
                    "SetToEndgameText",
                    Icons.Material.Rounded.Flag,
                    ToolKind.SetToEndgame
                ),
                new(
                    "WorldItemCloner",
                    "WorldItemClonerText",
                    Icons.Material.Rounded.ContentCopy,
                    ToolKind.WorldItemCloner
                ),
            ]
        ),
        new(
            "ToolsPlayer",
            [
                new(
                    "ConsumedItems",
                    "ConsumedItemsText",
                    Icons.Material.Rounded.Restaurant,
                    ToolKind.ConsumedItems
                ),
                new(
                    "ItemPlater",
                    "ItemPlaterText",
                    Icons.Material.Rounded.AutoAwesome,
                    ToolKind.ItemPlater
                ),
                new("Unlocks", "UnlocksText", Icons.Material.Rounded.LockOpen, ToolKind.Unlocks),
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
        return HasWorld || !IsWorldTool(tool.Kind);
    }

    private static bool IsWorldTool(ToolKind kind)
    {
        return kind is not (ToolKind.ConsumedItems or ToolKind.ItemPlater or ToolKind.Unlocks);
    }

    private string CardClass(Tool tool)
    {
        return IsAvailable(tool)
            ? "rounded-panel pa-4 mud-height-full hover-card"
            : "rounded-panel pa-4 mud-height-full tool-unavailable";
    }

    private string ChipKey(Tool tool)
    {
        if (!IsAvailable(tool))
        {
            return "HostSaveOnly";
        }

        return tool.Kind is ToolKind.ItemPlater or ToolKind.Unlocks ? "SavesAtOnce" : "NeedsSave";
    }

    private Color ChipColor(Tool tool)
    {
        return ChipKey(tool) == "SavesAtOnce" ? Color.Warning : Color.Default;
    }

    private async Task RunAsync(Tool tool)
    {
        if (!IsAvailable(tool))
        {
            return;
        }

        var stateBefore = MainViewModel.ToolState();
        switch (tool.Kind)
        {
            case ToolKind.RegrowTrees:
                await ShowDialogAsync<RegrowTreesDialog>();
                break;
            case ToolKind.CannibalAnger:
                await ShowDialogAsync<CannibalAngerDialog>();
                break;
            case ToolKind.WorldItemCloner:
                await ShowDialogAsync<WorldItemClonerDialog>();
                break;
            case ToolKind.ConsumedItems:
                await ShowDialogAsync<ConsumedItemsDialog>();
                break;
            case ToolKind.ItemPlater:
                await ShowDialogAsync<ItemPlaterDialog>();
                break;
            case ToolKind.Unlocks:
                await ShowDialogAsync<UnlocksDialog>();
                break;
            default:
                await ConfirmAndRunAsync(tool);
                break;
        }

        MainViewModel.FinishTool(tool.TitleKey, stateBefore);
    }

    private async Task ConfirmAndRunAsync(Tool tool)
    {
        if (!await ConfirmAsync(tool.TitleKey, tool.TextKey))
        {
            return;
        }

        var message = tool.Kind switch
        {
            ToolKind.EternalFires => MainViewModel.EternalFires(),
            ToolKind.ResetFires => MainViewModel.ResetFires(),
            ToolKind.ResetStructureDamage => MainViewModel.ResetStructureDamage(),
            ToolKind.ResetTraps => MainViewModel.ResetTraps(),
            ToolKind.ResetContainers => GameStatePage.ResetContainers(),
            _ => SetToEndgame(),
        };
        if (!string.IsNullOrEmpty(message))
        {
            Snackbar.Add(message, Severity.Info);
        }
    }

    private string SetToEndgame()
    {
        GameStatePage.SetToEndgame();
        return Localizer["ChangesAdded"];
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

    private enum ToolKind
    {
        RegrowTrees,
        EternalFires,
        ResetFires,
        ResetStructureDamage,
        ResetTraps,
        ResetContainers,
        CannibalAnger,
        SetToEndgame,
        WorldItemCloner,
        ConsumedItems,
        ItemPlater,
        Unlocks,
    }

    private sealed record Tool(string TitleKey, string TextKey, string Icon, ToolKind Kind);

    private sealed record ToolSection(string TitleKey, Tool[] Tools);
}
