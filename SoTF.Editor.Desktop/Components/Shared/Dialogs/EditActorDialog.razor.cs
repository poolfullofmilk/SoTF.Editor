using Microsoft.AspNetCore.Components;
using MudBlazor;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.Actors;
using SoTF.Editor.Data.ViewModels;
using SoTF.Editor.Services.Actors;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Desktop.Components.Shared.Dialogs;

public partial class EditActorDialog
{
    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }

    [Parameter]
    public required Actor Actor { get; set; }

    [Inject]
    public required GameData GameData { get; set; }

    [Inject]
    public required ActorModifier ActorModifier { get; set; }

    [Inject]
    public required MainViewModel MainViewModel { get; set; }

    [Inject]
    public required ISnackbar Snackbar { get; set; }

    private EditActorViewModel _viewModel = null!;

    private IEnumerable<ActorDetail> Details =>
        [
            new(AppText.Get("Classification"), _viewModel.Actor.ActorType?.PrintableClassification),
            new(AppText.Get("Gender"), _viewModel.Actor.ActorType?.PrintableGender),
            new(AppText.Get("State"), _viewModel.Actor.StatePrintable),
            new(AppText.Get("UniqueId"), _viewModel.Actor.UniqueId.ToString()),
            new(AppText.Get("Family"), _viewModel.Actor.FamilyId?.ToString()),
            new(AppText.Get("LastVisitTime"), _viewModel.Actor.LastVisitTime?.ToString()),
            new(AppText.Get("SpawnedCount"), _viewModel.Actor.Spawner?.Count.ToString()),
            new(AppText.Get("LastSpawnTime"), _viewModel.Actor.Spawner?.LastSpawnTime.ToString()),
            new(
                AppText.Get("KilledByPlayer"),
                (_viewModel.Actor.KillStats?.PlayerKilled ?? 0).ToString()
            ),
            new(AppText.Get("Area"), _viewModel.Actor.Position?.Area.Name),
            new(AppText.Get("Position"), _viewModel.Actor.Position?.PrintableShort),
        ];

    public static async Task ShowAsync(IDialogService dialogService, Actor actor)
    {
        var dialog = await dialogService.ShowAsync<EditActorDialog>(
            null,
            new DialogParameters<EditActorDialog>
            {
                { editActorDialog => editActorDialog.Actor, actor },
            },
            new DialogOptions { MaxWidth = MaxWidth.Large, FullWidth = true }
        );
        await dialog.Result;
    }

    protected override void OnInitialized()
    {
        _viewModel = new EditActorViewModel(
            Actor,
            [.. GameData.ActorTypes.OrderBy(type => type.Name)]
        );
    }

    private static string SelectionName(short? selection)
    {
        return EditActorViewModel
                .AllActorSelections.FirstOrDefault(pair => pair.Key == selection)
                .Value
            ?? "";
    }

    private void Apply()
    {
        MainViewModel.ModifyActors(savegame => ActorModifier.Modify(savegame, _viewModel));
        Snackbar.Add(AppText.Get("ChangesAdded"), Severity.Info);
        MudDialog.Close(DialogResult.Ok(true));
    }

    private sealed record ActorDetail(string Label, string? Value);
}
