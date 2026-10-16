using System.Collections.Concurrent;

namespace SoTF.Editor.Data.Models;

public class AreaMaskManager
{
    public static readonly Area Surface = new("Surface", 0, 1);
    private readonly ConcurrentDictionary<int, Area> _areaMasks = new();
    private readonly ConcurrentDictionary<int, Area> _graphMasks = new();

    public AreaMaskManager(List<Area> areas)
    {
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

    public Area GetAreaForGraphMask(int graphMask)
    {
        return _graphMasks.GetOrAdd(graphMask, mask => new Area($"??? ({mask})", mask, mask));
    }

    public IEnumerable<Area> GetAllAreas()
    {
        return new HashSet<Area>(_areaMasks.Values.Concat(_graphMasks.Values));
    }
}
