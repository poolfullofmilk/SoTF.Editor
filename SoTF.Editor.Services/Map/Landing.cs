using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.Map;

namespace SoTF.Editor.Services.Map;

public sealed class Landing(BasePoi destination, float offset, AreaMaskManager areaMaskManager)
{
    // A Point Without A Height Starts At Zero
    public float X { get; } = destination.Position?.X ?? destination.X;
    public float Y { get; } = destination.Position?.Y ?? 0;
    public float Z { get; } = destination.Position?.Z ?? destination.Z;

    public Area SelectedArea { get; set; } = destination.Position?.Area ?? AreaMaskManager.Surface;

    public float XOffset { get; set; } = offset;

    public float YOffset { get; set; } = offset;

    public float ZOffset { get; set; } = offset;

    public List<Area> Areas { get; } = [.. areaMaskManager.GetAllAreas()];

    public Position At(int step = 0)
    {
        return new Position(X + XOffset + step, Y + YOffset, Z + ZOffset + step)
        {
            Area = SelectedArea,
        };
    }
}
