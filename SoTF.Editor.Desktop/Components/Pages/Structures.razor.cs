using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Desktop.Components.Shared.Common;
using SoTF.Editor.Desktop.Components.Shared.Dialogs;
using SoTF.Editor.Services.Map;
using SoTF.Editor.Services.Pages;

namespace SoTF.Editor.Desktop.Components.Pages;

public partial class Structures : EditorPageBase
{
    // Keep First, Then From Most Built To Gone
    private static readonly ScrewStructureModificationMode[] s_modes =
    [
        ScrewStructureModificationMode.None,
        ScrewStructureModificationMode.Finish,
        ScrewStructureModificationMode.AlmostFinish,
        ScrewStructureModificationMode.Unfinish,
        ScrewStructureModificationMode.Remove,
    ];

    [Inject]
    public required StructuresPageViewModel StructuresPage { get; set; }

    // Page State
    private string? _filter;
    private string? _selectedName;
    private bool _showAll;

    // One Menu For Every Row, Row Selects Leaked Popovers
    private MudMenu? _modeMenu;
    private ScrewStructureWrapper? _menuStructure;

    // One Row Per Structure Type, By Category Then Name
    private List<StructureType> FilteredTypes()
    {
        return
        [
            .. StructuresPage
                .Structures.GroupBy(wrapper => wrapper.Name)
                .Select(group => new StructureType(
                    group.Key,
                    group.First().Category,
                    IconOf(group.First()),
                    [.. group.OrderBy(wrapper => wrapper.Origin).ThenBy(wrapper => wrapper.PctDone)]
                ))
                .Where(type => Matches(type.Name) || Matches(type.Category))
                .OrderBy(type => type.Category)
                .ThenBy(type => type.Name),
        ];
    }

    // Only A Shown Type, A Search Can Hide It
    private List<ScrewStructureWrapper>? ShownIn(List<StructureType> types)
    {
        return _showAll
            ? [.. types.SelectMany(type => type.Structures)]
            : types.FirstOrDefault(type => type.Name == _selectedName)?.Structures;
    }

    private string ShownTitle => _showAll ? AppText.Get("AllStructures") : _selectedName ?? "";

    private bool Matches(string text)
    {
        return string.IsNullOrWhiteSpace(_filter)
            || text.Contains(_filter, StringComparison.OrdinalIgnoreCase);
    }

    private static string RowClass(bool selected)
    {
        return "list-row px-4 py-2" + (selected ? " list-row-selected" : "");
    }

    private void SelectAll()
    {
        _showAll = true;
        _selectedName = null;
    }

    private void Select(StructureType type)
    {
        _showAll = false;
        _selectedName = type.Name;
    }

    private static string CountsOf(IEnumerable<ScrewStructureWrapper> structures)
    {
        var built = 0;
        var unfinished = 0;
        foreach (var structure in structures)
        {
            _ = structure.Origin == ScrewStructureOrigin.Finished ? built++ : unfinished++;
        }

        // A Count Of None Says Nothing, So It Stays Out
        return Formatter.JoinParts(
            built > 0 ? AppText.Format("StructureCountBuilt", built) : null,
            unfinished > 0 ? AppText.Format("StructureCountUnfinished", unfinished) : null
        );
    }

    private static string StateText(ScrewStructureWrapper wrapper)
    {
        if (wrapper.Origin == ScrewStructureOrigin.Finished)
        {
            return AppText.Get("Built");
        }

        // How Far Along, Or Just Pieces For Unknown Costs
        return wrapper.PctDone < 0
            ? AppText.Format("UnfinishedAdded", wrapper.Added)
            : AppText.Format(
                "UnfinishedState",
                wrapper.PctDonePrintable,
                wrapper.Added,
                wrapper.BuildCost
            );
    }

    private static string? LockedReason(ScrewStructureWrapper wrapper)
    {
        if (wrapper.ModificationModes.Count > 1)
        {
            return null;
        }

        // A Greyed Mode Says Why, Ziplines Change On The Map
        return AppText.Get(
            wrapper.ScrewStructure?.Id == ZiplineManager.ZiplineTypeId
                ? "ZiplinesOnMap"
                : "StructureUnknown"
        );
    }

    private static Color StateColor(ScrewStructureWrapper wrapper)
    {
        // Unknown, Barely Started, Under Way, Done
        return wrapper.PctDone switch
        {
            -1 => Color.Default,
            < 50 => Color.Error,
            < 100 => Color.Warning,
            _ => Color.Success,
        };
    }

    private static string ModeName(ScrewStructureModificationMode mode)
    {
        return AppText.Get(TextKeys.StructureMode(mode));
    }

    private static string? IconOf(ScrewStructureWrapper wrapper)
    {
        return wrapper.ScrewStructure?.Icon is { } icon ? $"images/structures/{icon}" : null;
    }

    private async Task SetAllAsync(
        List<ScrewStructureWrapper> structures,
        ScrewStructureModificationMode mode
    )
    {
        if (
            mode == ScrewStructureModificationMode.Remove
            && !await ConfirmAsync("SetAllToRemove", "SetAllToRemoveText")
        )
        {
            return;
        }

        StructuresPageViewModel.SetModificationMode(structures, mode);
    }

    private async Task OpenModesAsync(MouseEventArgs args, ScrewStructureWrapper wrapper)
    {
        // The Menu Opens Where The Row Was Clicked
        _menuStructure = wrapper;
        if (_modeMenu is { } modeMenu)
        {
            await modeMenu.OpenMenuAsync(args);
        }
    }

    private void SetMode(ScrewStructureModificationMode mode)
    {
        _menuStructure?.ModificationMode = mode;
    }

    private async Task ChangeTypeAsync(ScrewStructureWrapper wrapper)
    {
        var parameters = new DialogParameters<ChangeStructureTypeDialog>
        {
            { dialog => dialog.Structure, wrapper },
        };
        var dialog = await DialogService.ShowAsync<ChangeStructureTypeDialog>(
            AppText.Get("ChangeType"),
            parameters,
            new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true }
        );

        if (await dialog.Result is { Canceled: false, Data: ScrewStructure selected })
        {
            wrapper.Update(selected);
        }
    }

    private sealed record StructureType(
        string Name,
        string Category,
        string? Icon,
        List<ScrewStructureWrapper> Structures
    );
}
