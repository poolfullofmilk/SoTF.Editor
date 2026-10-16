using Microsoft.AspNetCore.Components;
using SoTF.Editor.Desktop.Components.Shared.Common;
using SoTF.Editor.Services.Pages;

namespace SoTF.Editor.Desktop.Components.Pages;

public partial class Followers : EditorPageBase
{
    [Inject]
    public required FollowerPageViewModel FollowerPage { get; set; }
}
