using System.Collections.Concurrent;

namespace SoTF.Editor.Data.Models;

public class AreaMaskManager
{
    public static readonly Area Surface = new("Surface", 0, 1);
    private readonly ConcurrentDictionary<int, Area> _areaMasks = new();
    private readonly ConcurrentDictionary<int, Area> _graphMasks = new();
    private readonly List<Area> _areas;

    public AreaMaskManager(List<Area> areas)
    {
        _areas = areas;
        foreach (var area in areas)
        {
            _areaMasks.TryAdd(area.AreaMask, area);
            _graphMasks.TryAdd(area.GraphMask, area);
        }
    }

    public Area GetAreaForAreaMask(int areaMask)
    {
        return _areaMasks.GetOrAdd(areaMask, mask => new Area($"??? ({mask})", mask, mask));
    }

    public Area? KnownArea(int areaMask)
    {
        // Only Areas Seen So Far, An Unknown Mask Adds Nothing
        return _areaMasks.GetValueOrDefault(areaMask);
    }

    public List<Area> CavesSharingGraphMask(Area area)
    {
        // Caves Sharing One Graph Mask, Like F And G
        if (!area.IsUnderground())
        {
            return [];
        }

        var caves = _areas
            .Where(other => other.IsUnderground() && other.GraphMask == area.GraphMask)
            .ToList();
        return caves.Count > 1 ? caves : [];
    }

    public Area GetAreaForGraphMask(int graphMask)
    {
        return _graphMasks.GetOrAdd(graphMask, mask => new Area($"??? ({mask})", mask, mask));
    }

    public IEnumerable<Area> GetAllAreas()
    {
        // The Surface First, Then Caves And Bunkers By Name
        return _areaMasks
            .Values.Concat(_graphMasks.Values)
            .Distinct()
            .OrderBy(area => area.IsUnderground())
            .ThenBy(area => area.Name);
    }
}
