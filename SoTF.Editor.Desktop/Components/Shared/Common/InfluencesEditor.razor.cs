using Microsoft.AspNetCore.Components;
using SoTF.Editor.Data.Models.SaveData.Actor;

namespace SoTF.Editor.Desktop.Components.Shared.Common;

public partial class InfluencesEditor
{
    [Inject]
    public required IStringLocalizer<SharedResource> Localizer { get; set; }

    [Parameter]
    public required List<Influence> Influences { get; set; }

    [Parameter]
    public bool ShowDisclaimer { get; set; }

    [Parameter]
    public bool CanRemove { get; set; }

    // Tells The Owner The Influences Need Saving
    [Parameter]
    public EventCallback OnChanged { get; set; }

    private string? _newType;

    private IEnumerable<string> MissingTypes =>
        Influence.AllTypes.Where(type => Influences.All(influence => influence.TypeId != type));

    private string TypeName(string? type)
    {
        return type is null ? string.Empty : Localizer[TextKeys.Influence(type)];
    }

    private async Task Change(Action change)
    {
        change();
        await OnChanged.InvokeAsync();
    }

    private async Task Add()
    {
        if (_newType is { } type)
        {
            Influences.Add(Influence.AsFillerWithDefaults(type));
            _newType = null;
            await OnChanged.InvokeAsync();
        }
    }

    private async Task Remove(Influence influence)
    {
        Influences.Remove(influence);
        await OnChanged.InvokeAsync();
    }
}
