namespace SoTF.Editor.Data.Models.Map.Static;

public sealed class Teleport(float x, float y, float z, int areaMask)
{
    public Position ToPosition(AreaMaskManager areaMaskManager)
    {
        return new Position(x, y, z) { Area = areaMaskManager.GetAreaForAreaMask(areaMask) };
    }
}
