using SoTF.Editor.Data.Enums;

namespace SoTF.Editor.Data.Models.Map;

public class WorldItemPoi(WorldItemState state) : BasePoi(state.Position)
{
    public WorldItemState State { get; } = state;

    public override string Icon => IconOf(State.WorldItemType);

    public static string IconOf(WorldItemType type) =>
        type switch
        {
            WorldItemType.HangGlider => "images/worldobjects/hang-gliding.svg",
            WorldItemType.KnightV => "images/worldobjects/monowheel.svg",
            WorldItemType.GolfCart => "images/worldobjects/golf-cart.svg",
            WorldItemType.Radio => "images/worldobjects/radio.svg",
            _ => DefaultIcon,
        };

    public override string Title => State.ObjectName;
}
