using Microsoft.AspNetCore.Components;
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
    public bool Changed { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public string? Label { get; set; }
}
