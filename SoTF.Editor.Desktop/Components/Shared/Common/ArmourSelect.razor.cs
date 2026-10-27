using Microsoft.AspNetCore.Components;
using MudBlazor;
using SoTF.Editor.Data.Models;

namespace SoTF.Editor.Desktop.Components.Shared.Common;

public partial class ArmourSelect
{
    [Parameter]
    public Item? Value { get; set; }

    [Parameter]
    public EventCallback<Item?> ValueChanged { get; set; }

    [Parameter]
    public required IEnumerable<Item> Types { get; set; }

    [Parameter]
    public string? Label { get; set; }

    [Parameter]
    public bool Changed { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    // Shown While No Type Is Picked
    [Parameter]
    public string EmptyIcon { get; set; } = Icons.Material.Rounded.Shield;
}
