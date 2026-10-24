namespace SoTF.Editor.Data.Models;

public class Area(string name, int areaMask, int graphMask)
{
    public string Name { get; } = name;
    public int AreaMask { get; } = areaMask;
    public int GraphMask { get; } = graphMask;

    public bool IsUnderground()
    {
        return AreaMask != 0;
    }

    public override bool Equals(object? obj)
    {
        return obj is Area other && AreaMask == other.AreaMask && GraphMask == other.GraphMask;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(AreaMask, GraphMask);
    }
}
