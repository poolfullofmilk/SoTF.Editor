using SoTF.Editor.Data.Models;
using SoTF.Editor.Data.Models.Map;

namespace SoTF.Editor.Services.Map;

public sealed class Landing(BasePoi destination, float lift)
{
    // A Point Without A Height Starts At Zero
    public float X { get; } = destination.Position?.X ?? destination.X;
    public float Y { get; } = destination.Position?.Y ?? 0;
    public float Z { get; } = destination.Position?.Z ?? destination.Z;

    // The Point's Own Area, A Clicked Spot Is Surface
    public Area Area { get; set; } = destination.Position?.Area ?? AreaMaskManager.Surface;

    // Set While A Clicked Spot Borrowed A Far Height
    public float? FarHeightSourceMetres { get; } =
        destination is LocationPoi { HeightIsFarGuess: true } spot ? spot.HeightSourceMetres : null;

    public Position At(int step = 0)
    {
        // Right On The Spot And A Little Above, Never Sideways
        return new Position(X + step, Y + lift, Z + step) { Area = Area };
    }
}
