using Newtonsoft.Json.Linq;

namespace SoTF.Editor.Data.Models.Map;

public class ZiplinePoi : IPoi
{
    public ZiplinePoi(JToken token, Position posA, Position posB)
    {
        Token = token;
        PointA = new ZipPointPoi(posA, this);
        PointB = new ZipPointPoi(posB, this);
    }

    public JToken Token { get; set; }

    public ZipPointPoi PointA { get; }
    public ZipPointPoi PointB { get; }

    public bool Enabled { get; set; }

    public bool Filtered { get; private set; }

    public bool IsSelected { get; set; }

    public bool Visible => Enabled && !Filtered;

    public Position? Position => null;
    public string? AreaName => null;
    public string Icon => "";
    public float MapLeft => 0;
    public float MapTop => 0;
    public string Title => "";
    public string Description => "";

    public int IconSize => 0;
    public int IconZIndex => -1;
    public float IconRotation => 0;

    public void ApplyFilter(MapFilter mapFilter)
    {
        // The Line Hides With Either Anchor, Search And Area Included
        PointA.ApplyFilter(mapFilter);
        PointB.ApplyFilter(mapFilter);
        Filtered = PointA.Filtered || PointB.Filtered;
    }

    public string PrintableCoordinates => "";
    public bool IsUnderground => false;
}
