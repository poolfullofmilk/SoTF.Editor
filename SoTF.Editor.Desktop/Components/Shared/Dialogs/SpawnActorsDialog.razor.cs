using Microsoft.AspNetCore.Components;
using MudBlazor;
using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.Map;
using SoTF.Editor.Services.Map;
using SoTF.Editor.Services.Pages;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Desktop.Components.Shared.Dialogs;

public partial class SpawnActorsDialog
{
    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }

    [Parameter]
    public required BasePoi Destination { get; set; }

    [Inject]
    public required GameData GameData { get; set; }

    [Inject]
    public required NpcsPageViewModel NpcsPage { get; set; }

    [Inject]
    public required MainViewModel MainViewModel { get; set; }

    [Inject]
    public required ISnackbar Snackbar { get; set; }

    private MapSpawnActorsViewModel _viewModel = null!;

    protected override void OnInitialized()
    {
        var allFamilyIds = NpcsPage
            .AllActors.Where(actor => actor.FamilyId != null)
            .Select(actor => actor.FamilyId!.Value)
            .Distinct()
            .OrderBy(id => id)
            .ToList();
        var allActorTypes = GameData.ActorTypes.OrderBy(type => type.Name).ToList();
        _viewModel = new MapSpawnActorsViewModel(
            Destination,
            allFamilyIds,
            allActorTypes,
            GameData.AreaManager
        );
    }

    private string FamilyName(int? familyId)
    {
        if (familyId is null or -1)
        {
            return "-";
        }

        // The Destination Actor's Own Family Reads As Such
        return familyId == _viewModel.OriginalFamilyId
            ? $"{familyId} ({AppText.Get("DestinationFamily")})"
            : familyId.ToString()!;
    }

    private static string PatternName(SpawnPattern pattern)
    {
        return AppText.Get(TextKeys.SpawnPattern(pattern));
    }

    private void Spawn()
    {
        MainViewModel.ModifyActors(savegame => _viewModel.DoSpawn(savegame));
        Snackbar.Add(AppText.Get("ChangesAdded"), Severity.Info);
        MudDialog.Close(DialogResult.Ok(true));
    }
}
