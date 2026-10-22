using Microsoft.AspNetCore.Components;
using MudBlazor;
using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Desktop.Components.Shared.Common;
using SoTF.Editor.Desktop.Components.Shared.Dialogs;
using SoTF.Editor.Services.Pages;

namespace SoTF.Editor.Desktop.Components.Pages;

public partial class Structures : EditorPageBase
{
    private static readonly TableGroupDefinition<ScrewStructureWrapper> s_groupByName = new()
    {
        Selector = wrapper => wrapper.Name,
    };

    // The Batch Buttons Each Origin Offered Before
    private static readonly OriginBatch[] s_origins =
    [
        new(
            ScrewStructureOrigin.Unfinished,
            "UnfinishedStructures",
            [
                new(ScrewStructureModificationMode.None, "ResetAll"),
                new(ScrewStructureModificationMode.Remove, "SetAllToRemove"),
                new(ScrewStructureModificationMode.AlmostFinish, "SetAllToAlmostFinish"),
                new(ScrewStructureModificationMode.Finish, "SetAllToFinish"),
                new(ScrewStructureModificationMode.Unfinish, "SetAllToUnfinish"),
            ]
        ),
        new(
            ScrewStructureOrigin.Finished,
            "FinishedStructures",
            [
                new(ScrewStructureModificationMode.None, "ResetAll"),
                new(ScrewStructureModificationMode.Remove, "SetAllToRemove"),
                new(ScrewStructureModificationMode.AlmostFinish, "SetAllToAlmostFinish"),
                new(ScrewStructureModificationMode.Unfinish, "SetAllToUnfinish"),
            ]
        ),
    ];

    [Inject]
    public required StructuresPageViewModel StructuresPage { get; set; }

    private string? _filter;

    // Every Text Column Counts, The Modification Select Does Not
    private IEnumerable<ScrewStructureWrapper> FilteredStructures =>
        StructuresPage
            .Structures.Where(wrapper =>
                string.IsNullOrWhiteSpace(_filter)
                || new[]
                {
                    wrapper.Name,
                    OriginName(wrapper),
                    wrapper.Category,
                    AddedText(wrapper),
                    wrapper.PctDonePrintable,
                }.Any(text => text.Contains(_filter, StringComparison.OrdinalIgnoreCase))
            )
            .OrderBy(wrapper => wrapper.Name)
            .ThenBy(wrapper => wrapper.PctDone);

    private string OriginName(ScrewStructureWrapper wrapper)
    {
        return Localizer[
            wrapper.Origin == ScrewStructureOrigin.Finished
                ? "FinishedStructures"
                : "UnfinishedStructures"
        ];
    }

    private static string AddedText(ScrewStructureWrapper wrapper)
    {
        return $"{wrapper.Added} / {wrapper.BuildCost}";
    }

    private bool HasBatchTargets(ScrewStructureOrigin origin)
    {
        return StructuresPage.BatchTargets(FilteredStructures, origin).Any();
    }

    private string ModeName(ScrewStructureModificationMode? mode)
    {
        return Localizer[TextKeys.StructureMode(mode ?? ScrewStructureModificationMode.None)];
    }

    public static string StructureName(ScrewStructure? structure)
    {
        return structure is null ? string.Empty : $"{structure.CategoryName} · {structure.Name}";
    }

    private static Color ProgressColor(ScrewStructureWrapper wrapper)
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

    private static string? IconOf(ScrewStructureWrapper wrapper)
    {
        return wrapper.ScrewStructure?.Icon is { } icon ? $"images/structures/{icon}" : null;
    }

    private async Task SetAllAsync(ScrewStructureOrigin origin, ScrewStructureModificationMode mode)
    {
        if (
            mode == ScrewStructureModificationMode.Remove
            && !await ConfirmAsync("SetAllToRemove", "SetAllToRemoveText")
        )
        {
            return;
        }

        StructuresPage.SetModificationMode(FilteredStructures, origin, mode);
    }

    private async Task ChangeTypeAsync(ScrewStructureWrapper wrapper)
    {
        var parameters = new DialogParameters<ChangeStructureTypeDialog>
        {
            { dialog => dialog.Structure, wrapper },
        };
        var dialog = await DialogService.ShowAsync<ChangeStructureTypeDialog>(
            Localizer["ChangeType"],
            parameters,
            new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true }
        );

        if (await dialog.Result is { Canceled: false, Data: ScrewStructure selected })
        {
            wrapper.Update(selected);
        }
    }

    private sealed record BatchMode(ScrewStructureModificationMode Mode, string LabelKey);

    private sealed record OriginBatch(
        ScrewStructureOrigin Origin,
        string TitleKey,
        BatchMode[] Batches
    );
}
