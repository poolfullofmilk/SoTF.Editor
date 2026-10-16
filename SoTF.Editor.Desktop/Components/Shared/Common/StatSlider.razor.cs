using Microsoft.AspNetCore.Components;

namespace SoTF.Editor.Desktop.Components.Shared.Common;

public partial class StatSlider
{
    [Parameter]
    public required string Label { get; set; }

    [Parameter]
    public float Value { get; set; }

    [Parameter]
    public EventCallback<float> ValueChanged { get; set; }

    [Parameter]
    public float Min { get; set; }

    [Parameter]
    public float Max { get; set; } = 100;

    [Parameter]
    public float Step { get; set; } = 1;
}
