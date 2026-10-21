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
    [Inject]
    public required IStringLocalizer<SharedResource> Localizer { get; set; }

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
            new(Localizer["Classification"], _viewModel.Actor.ActorType?.PrintableClassification),
            new(Localizer["Gender"], _viewModel.Actor.ActorType?.PrintableGender),
            new(Localizer["State"], _viewModel.Actor.StatePrintable),
            new(Localizer["UniqueId"], _viewModel.Actor.UniqueId.ToString()),
            new(Localizer["Family"], _viewModel.Actor.FamilyId?.ToString()),
            new(Localizer["LastVisitTime"], _viewModel.Actor.LastVisitTime?.ToString()),
            new(Localizer["SpawnedCount"], _viewModel.Actor.Spawner?.Count.ToString()),
            new(Localizer["LastSpawnTime"], _viewModel.Actor.Spawner?.LastSpawnTime.ToString()),
            new(
                Localizer["KilledByPlayer"],
                (_viewModel.Actor.KillStats?.PlayerKilled ?? 0).ToString()
            ),
            new(Localizer["Area"], _viewModel.Actor.Position?.Area.Name),
            new(Localizer["Position"], _viewModel.Actor.Position?.PrintableShort),
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
        // Empty First, So Replace Type Starts At No Change
        var allActorTypes = GameData.ActorTypes.OrderBy(type => type.Name).ToList();
        allActorTypes.Insert(0, new EmptyActorType());
        _viewModel = new EditActorViewModel(Actor, allActorTypes);
    }

    private static string ReplaceTypeName(ActorType? actorType)
    {
        // The Empty Choice Keeps Every Type As It Is
        return actorType is null or { Id: -1 } ? "—" : actorType.Name;
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
        Snackbar.Add(Localizer["ChangesAdded"], Severity.Info);
        MudDialog.Close(DialogResult.Ok(true));
    }

    private sealed record ActorDetail(string Label, string? Value);
}
