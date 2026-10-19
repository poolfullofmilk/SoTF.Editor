using Microsoft.AspNetCore.Components;
using MudBlazor;
using SoTF.Editor.Data.Models.Actors;
using SoTF.Editor.Desktop.Components.Shared.Common;
using SoTF.Editor.Desktop.Components.Shared.Dialogs;
using SoTF.Editor.Services.Pages;

namespace SoTF.Editor.Desktop.Components.Pages;

public partial class Npcs : EditorPageBase
{
    [Inject]
    public required NpcsPageViewModel NpcsPage { get; set; }

    // Page State
    private bool _byFamily;
    private ActorCollection? _selected;

    private IEnumerable<CollectionGroup> Groupings =>
        _byFamily
            ? NpcsPage.ActorsByFamily.Select(grouping => new CollectionGroup(
                grouping.Name,
                grouping.ActorCollections
            ))
            : [new CollectionGroup(null, NpcsPage.ActorsByType)];

    // A Reload Builds New Collections, An Old Selection Is Gone
    private ActorCollection? Selected =>
        _selected != null
        && (
            NpcsPage.ActorsByType.Contains(_selected)
            || NpcsPage.ActorsByFamily.Any(grouping =>
                grouping.ActorCollections.Contains(_selected)
            )
        )
            ? _selected
            : null;

    private string CollectionRowClass(ActorCollection collection)
    {
        var selected = _selected == collection ? " list-row-selected" : "";
        return "list-row px-4 py-2" + selected;
    }

    private async Task EditAsync(Actor actor)
    {
        await EditActorDialog.ShowAsync(DialogService, actor);
    }

    private sealed record CollectionGroup(string? Title, List<ActorCollection> Collections);
}
