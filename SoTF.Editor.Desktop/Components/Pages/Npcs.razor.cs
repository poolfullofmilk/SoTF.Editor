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
    private string? _filter;

    // A Matching Group Shows Whole, Otherwise Only Its Matching Rows
    private IEnumerable<CollectionGroup> Groupings =>
        (
            _byFamily
                ? NpcsPage.ActorsByFamily.Select(grouping => new CollectionGroup(
                    grouping.Name,
                    grouping.ActorCollections
                ))
                : [new CollectionGroup(null, NpcsPage.ActorsByType)]
        )
            .Select(group =>
                group.Title is { } title && Matches(title)
                    ? group
                    : group with
                    {
                        Collections = [.. group.Collections.Where(Matches)],
                    }
            )
            .Where(group => group.Collections.Count > 0);

    private bool Matches(ActorCollection collection)
    {
        // A Family Row Also Matches The Creatures In It
        return Matches(collection.Name)
            || collection.Actors.Any(actor => Matches(actor.ActorType?.Name ?? ""));
    }

    private bool Matches(string text)
    {
        return string.IsNullOrWhiteSpace(_filter)
            || text.Contains(_filter, StringComparison.OrdinalIgnoreCase);
    }

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

    private static string DetailOf(Actor actor)
    {
        // A Missing Gender Leaves Out Its Part, Not A Dash
        return string.Join(
            " · ",
            new[]
            {
                actor.ActorType?.PrintableClassification,
                actor.ActorType?.PrintableGender,
            }.Where(part => part is { Length: > 0 } and not "-")
        );
    }

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
