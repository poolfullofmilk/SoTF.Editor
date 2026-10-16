namespace SoTF.Editor.Data.Models;

public class PlayerState
{
    public float CurrentHealth { get; set; }

    public float Fullness { get; set; }

    public float FullnessBuff { get; set; }

    public float Hydration { get; set; }

    public float HydrationBuff { get; set; }

    public float MaxHealth
    {
        get;
        set
        {
            field = value;
            if (CurrentHealth > value)
            {
                CurrentHealth = value;
            }
        }
    }

    public Position Pos { get; set; } = new(0, 0, 0);

    public float Rest { get; set; }

    public float RestBuff { get; set; }

    public Item? SelectedCloth { get; set; }

    public float Sickness { get; set; }

    public float Stamina { get; set; }

    public int StrengthLevel { get; set; }

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
