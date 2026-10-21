using Microsoft.AspNetCore.Components;
using MudBlazor;
using SoTF.Editor.Desktop.Components.Layout;
using SoTF.Editor.Desktop.Components.Pages;
using SoTF.Editor.Services.Pages;
using SoTF.Editor.Services.Savegames;

namespace SoTF.Editor.Desktop.Components.Shared.Dialogs;

public partial class SaveChangesDialog
{
    [Inject]
    public required IStringLocalizer<SharedResource> Localizer { get; set; }

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

    // A Long List Ends In A Count
    private const int ShownChanges = 6;

    private List<string> ChangesOf(string page)
    {
        var distinct = AllChangesOf(page).Distinct().Order().ToList();
        return distinct.Count <= ShownChanges
            ? distinct
            :
            [
                .. distinct.Take(ShownChanges - 1),
                Localizer["AndMore", distinct.Count - ShownChanges + 1],
            ];
    }

    private IEnumerable<string> AllChangesOf(string page)
    {
        return page switch
        {
            "Player" => PlayerPage.ChangedNames.Concat(ArmorPage.ChangedNames),
            "Inventory" => InventoryPage.ChangedNames,
            "Storage" => StoragePage.ChangedNames,
            "Followers" => FollowerPage.ChangedNames,
            "Game" => GameSetup
                .ChangedKeys.Select(key => Localizer[Game.SetupLabelKey(key)].Value)
                .Concat(GameState.ChangedNames)
                .Concat(Weather.ChangedNames),
            "Npcs" => NpcsPage.ChangedNames,
            "Structures" => StructuresPage.ChangedNames,
            "Map" => [Localizer["Ziplines"].Value],
            "Tools" => MainViewModel.ChangedTools.Select(tool => Localizer[tool].Value),
            _ => [],
        };
    }

    private void Save()
    {
        MudDialog.Close(DialogResult.Ok(true));
    }
}
