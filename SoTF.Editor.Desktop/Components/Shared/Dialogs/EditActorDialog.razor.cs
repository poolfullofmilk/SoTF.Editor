using Microsoft.AspNetCore.Components;
using MudBlazor;
using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.Actors;
using SoTF.Editor.Data.ViewModels;
using SoTF.Editor.Services.Actors;
using SoTF.Editor.Services.Pages;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Desktop.Components.Shared.Dialogs;

public partial class EditActorDialog
{
    // Who It Reaches, Widest Last
    private static readonly Selection[] s_selections =
    [
        new(0, "ApplyToThis"),
        new(1, "ApplyToFamily"),
        new(2, "ApplyToType"),
        new(3, "ApplyToEveryone"),
    ];

    private static readonly TeleportOption[] s_teleports =
    [
        new("", "None"),
        new("NpcToPlayer", "TeleportNpcToPlayer"),
        new("PlayerToNpc", "TeleportPlayerToNpc"),
    ];

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
    public required NpcsPageViewModel NpcsPage { get; set; }

    [Inject]
    public required IDialogService DialogService { get; set; }

    [Inject]
    public required ISnackbar Snackbar { get; set; }

    [Inject]
    public required FollowerPageViewModel FollowerPage { get; set; }

    private EditActorViewModel _viewModel = null!;

    private int MatchCount => CountFor(_viewModel.ActorSelection);

    // A Filter Shows Only Where It Changes Who Is Reached
    private bool ShowsSameArea => _viewModel.LimitsToSameArea(_viewModel.ActorSelection);
    private bool ShowsSkipFollowers => _viewModel.CanSkipFollowers(_viewModel.ActorSelection);

    private bool ReachesSpawner =>
        NpcsPage.AllActors.Any(actor =>
            actor.SpawnerId != null && Reaches(actor, _viewModel.ActorSelection)
        );

    // The Game's Own Kelvin And Virginia Stay, As On Followers
    private bool RemovesGameFollower =>
        NpcsPage.AllActors.Any(actor =>
            Reaches(actor, _viewModel.ActorSelection)
            && FollowerPage.CopiesOf(actor.TypeId).FirstOrDefault()?.UniqueId == actor.UniqueId
        );

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

    private int CountFor(short selection)
    {
        // The Change's Own Rule, Counted On Loaded Actors
        return NpcsPage.AllActors.Count(actor => Reaches(actor, selection));
    }

    private bool Reaches(Actor actor, short selection)
    {
        return _viewModel.Matches(
            actor.TypeId,
            actor.FamilyId,
            actor.GraphMask,
            actor.UniqueId,
            selection
        );
    }

    private void Select(short selection)
    {
        _viewModel.ActorSelection = selection;

        // A Hidden Switch Changes Nothing
        if (!ReachesSpawner)
        {
            _viewModel.ModifyOptions.RemoveSpawner = false;
        }
    }

    private string CountLabel(string labelKey)
    {
        return MatchCount > 1
            ? AppText.Format(labelKey + "Count", MatchCount)
            : AppText.Get(labelKey);
    }

    private async Task RemoveAsync()
    {
        if (
            !await DialogService.ConfirmAsync(CountLabel("Remove"), AppText.Get("RemoveActorsText"))
        )
        {
            return;
        }

        _viewModel.ModificationMode = ActorModificationMode.Remove;
        Apply();
    }

    private void Apply()
    {
        MainViewModel.ModifyActors(savegame => ActorModifier.Modify(savegame, _viewModel));
        Snackbar.Add(AppText.Get("ChangesAdded"), Severity.Info);
        MudDialog.Close(DialogResult.Ok(true));
    }

    private sealed record ActorDetail(string Label, string? Value);

    private sealed record Selection(short Value, string LabelKey);

    private sealed record TeleportOption(string Mode, string LabelKey);
}
