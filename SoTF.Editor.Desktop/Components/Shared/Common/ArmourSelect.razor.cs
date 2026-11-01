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

    // Column Captions Start Where The Select Does
    public const string CaptionStyle = "flex:0 1 250px;min-width:0;padding-left:48px";
}
