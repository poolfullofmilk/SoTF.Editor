using Microsoft.AspNetCore.Components;
using MudBlazor;
using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.ViewModels;
using SoTF.Editor.Desktop.Components.Layout;
using SoTF.Editor.Desktop.Components.Pages;
using SoTF.Editor.Services.Pages;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Desktop.Components.Shared.Dialogs;

public partial class SaveChangesDialog
{
    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }

    [Inject]
    public required MainViewModel MainViewModel { get; set; }

    [Inject]
    public required PlayerPageViewModel PlayerPage { get; set; }

    [Inject]
    public required ArmorPageViewModel ArmorPage { get; set; }

    [Inject]
    public required InventoryPageViewModel InventoryPage { get; set; }

    [Inject]
    public required StoragePageViewModel StoragePage { get; set; }

    [Inject]
    public required FollowerPageViewModel FollowerPage { get; set; }

    [Inject]
    public required GameSetupPageViewModel GameSetup { get; set; }

    [Inject]
    public required GameStatePageViewModel GameState { get; set; }

    [Inject]
    public required WeatherPageViewModel Weather { get; set; }

    [Inject]
    public required NpcsPageViewModel NpcsPage { get; set; }

    [Inject]
    public required StructuresPageViewModel StructuresPage { get; set; }

    [Parameter]
    public required List<MainLayout.PageLink> Pages { get; set; }

    // Revert Shows What Goes Back Instead Of What Gets Saved
    [Parameter]
    public bool Revert { get; set; }

    // Worked Out Once, The Backup Keeps The Same List
    private Dictionary<string, List<Change>> _changes = [];

    protected override void OnInitialized()
    {
        _changes = Pages.ToDictionary(link => link.Href, link => ChangesOf(link.Href));
    }

    private List<Change> ChangesOf(string page)
    {
        // Added First, Then Changed, Then Removed
        return
        [
            .. AllChangesOf(page)
                .Distinct()
                .OrderBy(change => change.Kind)
                .ThenBy(change => change.Name),
        ];
    }

    private IEnumerable<Change> AllChangesOf(string page)
    {
        return page switch
        {
            "Player" => Named(PlayerPage.ChangedNames).Concat(ArmorPage.Changes),
            "Inventory" => InventoryPage.Changes,
            "Storage" => Named(StoragePage.ChangedNames),
            "Followers" => FollowerPage.Changes,
            "Game" => Named(
                GameSetup
                    .ChangedKeys.Select(key => AppText.Get(Game.SetupLabelKey(key)))
                    .Concat(GameState.ChangedNames)
                    .Concat(Weather.ChangedNames)
            ),
            "Npcs" => NpcsPage.Changes,
            "Structures" => Named(StructuresPage.ChangedNames),
            "Map" => [new Change(AppText.Get("Ziplines"))],
            "Tools" => Named(MainViewModel.ChangedTools.Select(tool => AppText.Get(tool))),
            _ => [],
        };
    }

    private static IEnumerable<Change> Named(IEnumerable<string> names)
    {
        return names.Select(name => new Change(name));
    }

    private void Save()
    {
        MudDialog.Close(DialogResult.Ok(_changes));
    }
}
