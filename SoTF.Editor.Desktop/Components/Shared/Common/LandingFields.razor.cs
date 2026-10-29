using Microsoft.AspNetCore.Components;
using SoTF.Editor.Services.Map;

namespace SoTF.Editor.Desktop.Components.Shared.Common;

public partial class LandingFields
{
    [Parameter]
    public required Landing Landing { get; set; }
}
