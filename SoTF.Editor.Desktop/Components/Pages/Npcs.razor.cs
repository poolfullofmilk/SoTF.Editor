using Microsoft.AspNetCore.Components;
using MudBlazor;
using SoTF.Editor.Data.Helpers;
using SoTF.Editor.Data.Models.Actors;
using SoTF.Editor.Desktop.Components.Shared.Common;
using SoTF.Editor.Desktop.Components.Shared.Dialogs;
using SoTF.Editor.Services.Pages;

namespace SoTF.Editor.Desktop.Components.Pages;

public partial class Npcs : EditorPageBase
{
    [Inject]
    public required NpcsPageViewModel NpcsPage { get; set; }

    // Page State, Picked By Name To Outlive Actor Changes
    private bool _byFamily;
    private string? _selectedName;
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
            || Matches(DetailOf(collection.SharedType))
            || collection.Actors.Any(actor => Matches(actor.ActorType?.Name ?? ""));
    }

    private bool Matches(string text)
    {
        return string.IsNullOrWhiteSpace(_filter)
            || text.Contains(_filter, StringComparison.OrdinalIgnoreCase);
    }

    // Only A Shown Row, A Search Can Hide It
    private ActorCollection? Selected =>
        _selectedName == null
            ? null
            : Groupings
                .SelectMany(group => group.Collections)
                .FirstOrDefault(collection => collection.Name == _selectedName);

    private void SetByFamily(bool byFamily)
    {
        // The Other Grouping Has Other Rows, Nothing Stays Picked
        if (_byFamily != byFamily)
        {
            _byFamily = byFamily;
            _selectedName = null;
        }
    }

    private static string DetailOf(ActorType? actorType)
    {
        return Formatter.JoinParts(actorType?.PrintableClassification, actorType?.PrintableGender);
    }

    private string HeaderTitleOf(ActorCollection collection)
    {
        // A Family Is A Number, The Title Names It
        return _byFamily ? AppText.Format("FamilyTitle", collection.Name) : collection.Name;
    }

    private string CollectionRowClass(ActorCollection collection)
    {
        var selected = _selectedName == collection.Name ? " list-row-selected" : "";
        return "list-row px-4 py-2" + selected;
    }

    private async Task EditAsync(Actor actor)
    {
        await EditActorDialog.ShowAsync(DialogService, actor);
    }

    private sealed record CollectionGroup(string? Title, List<ActorCollection> Collections);
}
