namespace SoTF.Editor.Data.ViewModels;

public sealed record CannibalMood(
    bool AllKnowPlayer,
    IReadOnlyList<(float Anger, float Sentiment)> PlayerInfluences,
    bool HasWorldCounts
)
{
    public int Average =>
        PlayerInfluences.Count == 0
            ? 0
            : (int)Math.Round(PlayerInfluences.Average(influence => influence.Anger));

    public bool Changes(int level)
    {
        // Strangers, Others Off That Level, Or Counts Left At Calm
        return !AllKnowPlayer
            || PlayerInfluences.Any(influence =>
                Math.Abs(influence.Anger - level) >= 0.001
                || Math.Abs(influence.Sentiment + level) >= 0.001
            )
            || (level == 0 && HasWorldCounts);
    }
}
