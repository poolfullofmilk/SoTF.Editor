using Microsoft.AspNetCore.Components;
using MudBlazor;
using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.ViewModels;
using SoTF.Editor.Services.Savegames;
using SoTF.Editor.Services.Tools;

namespace SoTF.Editor.Desktop.Components.Shared.Dialogs;

public partial class GuestRolesDialog
{
    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }

    [Inject]
    public required MainViewModel MainViewModel { get; set; }

    [Inject]
    public required ISnackbar Snackbar { get; set; }

    // Weakest To Strongest, Banned Last
    private static readonly GuestRole[] s_roles =
    [
        GuestRole.Guest,
        GuestRole.Trusted,
        GuestRole.Admin,
        GuestRole.Banned,
    ];

    private List<Guest> _guests = [];
    private Dictionary<long, int> _roles = [];

    private bool HasChanges => _guests.Exists(guest => _roles[guest.SteamId] != guest.Role);

    private int? UnknownRoleOf(Guest guest)
    {
        var role = _roles[guest.SteamId];
        return Enum.IsDefined((GuestRole)role) ? null : role;
    }

    protected override void OnInitialized()
    {
        _guests = WorldTools.ReadGuests(MainViewModel.SelectedSavegame);
        _roles = _guests
            .DistinctBy(guest => guest.SteamId)
            .ToDictionary(guest => guest.SteamId, guest => guest.Role);
    }

    private void Apply()
    {
        Snackbar.Add(
            WorldTools.SetGuestRoles(MainViewModel.SelectedSavegame, _roles),
            Severity.Info
        );
        MudDialog.Close(DialogResult.Ok(true));
    }
}
