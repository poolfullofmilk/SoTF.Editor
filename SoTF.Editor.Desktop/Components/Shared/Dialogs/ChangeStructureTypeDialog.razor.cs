using Microsoft.AspNetCore.Components;
using MudBlazor;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Services.Pages;

namespace SoTF.Editor.Desktop.Components.Shared.Dialogs;

public partial class ChangeStructureTypeDialog
{
    [Inject]
    public required IStringLocalizer<SharedResource> Localizer { get; set; }

    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }

    [Parameter]
    public required ScrewStructureWrapper Structure { get; set; }

    [Inject]
    public required StructuresPageViewModel StructuresPage { get; set; }

    private ScrewStructure? _selected;

    // Free Or Locked Types Would Write A Broken Structure
    private IEnumerable<ScrewStructure> EditableTypes =>
        StructuresPage.StructureTypes.Where(structure => structure.CanEdit);

    protected override void OnInitialized()
    {
        _selected = StructuresPage.StructureTypes.FirstOrDefault(structure =>
            structure.Id == Structure.ScrewStructure?.Id
        );
    }

    private static string StructureName(ScrewStructure? structure)
    {
        return Pages.Structures.StructureName(structure);
    }

    private void Ok()
    {
        MudDialog.Close(DialogResult.Ok(_selected));
    }
}
