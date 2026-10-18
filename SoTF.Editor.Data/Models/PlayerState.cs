namespace SoTF.Editor.Data.Models;

public class PlayerState
{
    // Capped By Max Health, Comes Back When Max Rises
    public float CurrentHealth
    {
        get => Math.Min(field, MaxHealth);
        set;
    }

    public float Fullness { get; set; }

    public float FullnessBuff { get; set; }

    public float Hydration { get; set; }

    public float HydrationBuff { get; set; }

    public float MaxHealth { get; set; }

    public Position Pos { get; set; } = new(0, 0, 0);

    public float Rest { get; set; }

    public float RestBuff { get; set; }

    public Item? SelectedCloth { get; set; }

    public float Sickness { get; set; }

    public float Stamina { get; set; }

    public int StrengthLevel { get; set; }

    public void ChangeStrengthLevel(int level)
    {
        // The Game Adds Two Max Health Per Level Above One
        StrengthLevel = level;
        MaxHealth = 100 + (2 * Math.Max(level - 1, 0));
    }

    public void Reset()
    {
        Pos = new Position(0, 0, 0);
        StrengthLevel = 0;
        CurrentHealth = 0;
        MaxHealth = 0;
        Fullness = 0;
        FullnessBuff = 0;
        Hydration = 0;
        HydrationBuff = 0;
        RestBuff = 0;
        Rest = 0;
        Stamina = 0;
        Sickness = 0;
        SelectedCloth = null;
    }
}
