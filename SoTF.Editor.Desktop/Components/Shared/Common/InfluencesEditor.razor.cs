using Microsoft.AspNetCore.Components;
using MudBlazor;
using SoTF.Editor.Data.Models.SaveData.Actor;

namespace SoTF.Editor.Desktop.Components.Shared.Common;

public partial class InfluencesEditor
{
    // Sentiment, Anger And Fear Each Mood Sets At Once
    private static readonly Mood[] s_moods =
    [
        new("MoodFriendly", 100, 0, 0, Color.Success),
        new("MoodNeutral", 0, 0, 0, Color.Default),
        new("MoodHostile", -100, 100, 0, Color.Error),
        new("MoodAfraid", -100, 0, 100, Color.Warning),
    ];

    [Parameter]
    public required List<Influence> Influences { get; set; }

    [Parameter]
    public bool CanRemove { get; set; }

    // Tells The Owner The Influences Need Saving
    [Parameter]
    public EventCallback OnChanged { get; set; }

    [Parameter]
    public Func<Influence, string, bool>? ChangedWhen { get; set; }

    private string? _newType;

    private IEnumerable<string> MissingTypes =>
        Influence.AllTypes.Where(type => Influences.All(influence => influence.TypeId != type));

    private static Mood MoodOf(Influence influence)
    {
        // Fear Wins, Then Anger Or Dislike, Then Liking
        if (influence.Fear >= 50)
        {
            return s_moods[3];
        }

        if (influence.Anger >= 50 || influence.Sentiment <= -50)
        {
            return s_moods[2];
        }

        return influence.Sentiment >= 50 ? s_moods[0] : s_moods[1];
    }

    private bool IsChanged(Influence influence, string propertyName)
    {
        return ChangedWhen?.Invoke(influence, propertyName) == true;
    }

    private static string TypeName(string? type)
    {
        return type is null ? string.Empty : AppText.Get(TextKeys.Influence(type));
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
            Influences.Add(new Influence { TypeId = type });
            _newType = null;
            await OnChanged.InvokeAsync();
        }
    }

    private async Task Remove(Influence influence)
    {
        Influences.Remove(influence);
        await OnChanged.InvokeAsync();
    }

    private sealed record Mood(
        string LabelKey,
        float Sentiment,
        float Anger,
        float Fear,
        Color Color
    )
    {
        public bool IsExactly(Influence influence)
        {
            return influence.Sentiment == Sentiment
                && influence.Anger == Anger
                && influence.Fear == Fear;
        }

        public void ApplyTo(Influence influence)
        {
            influence.Sentiment = Sentiment;
            influence.Anger = Anger;
            influence.Fear = Fear;
        }
    }
}
