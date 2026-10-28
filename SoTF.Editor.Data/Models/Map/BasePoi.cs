using SoTF.Editor.Data.Helpers;

namespace SoTF.Editor.Data.Models.Map;

public abstract class BasePoi : IPoi
{
    private Position? _position;

    protected BasePoi(Position position)
        : this(position.X, position.Z, position) { }

    // The Marker And The Teleport Spot Can Differ
    protected BasePoi(float x, float y, Position? position)
    {
        X = x;
        Z = y;
        (MapLeft, MapTop) = CoordinateConverter.IngameToPixel(x, y);
        _position = position;
    }

    protected const string DefaultIcon = "images/icons/question-mark.svg";

    public float X { get; private set; }

    public float Z { get; private set; }

    private string NormalizedLowercaseTitle =>
        field ??= TranslationHelper.Normalize(Title).ToLowerInvariant();

    private string? NormalizedLowercaseDescription =>
        field ??= Description is { } description
            ? TranslationHelper.Normalize(description).ToLowerInvariant()
            : null;

    public Position? Position
    {
        get => _position;
        set
        {
            _position = value;
            if (value != null)
            {
                X = value.X;
                Z = value.Z;
                (MapLeft, MapTop) = CoordinateConverter.IngameToPixel(value.X, value.Z);
            }
        }
    }

    public bool Enabled { get; set; }

    public bool Filtered { get; protected set; }

    public bool IsSelected { get; set; }

    public string? AreaName => Position?.Area.Name;

    public string PrintableCoordinates =>
        Position != null
            ? $"X: {Position.X:F2}, Y: {Position.Y:F2}, Z: {Position.Z:F2}"
            : $"X: {X}, Z: {Z}";

    public virtual bool IsUnderground => Position?.Area.IsUnderground() ?? false;

    public bool Visible => Enabled && !Filtered;

    public abstract string Icon { get; }

    public virtual int IconSize => 32;

    // A Picked Icon Draws Again On Top
    public virtual int IconZIndex => 0;
    public float IconRotation => Position?.Rotation ?? 0;

    public float MapLeft { get; private set; }

    public float MapTop { get; private set; }

    public void ApplyFilter(MapFilter mapFilter)
    {
        Filtered = ShouldFilter(mapFilter);
    }

    public virtual string Title => "";

    public string? Description { get; init; }

    // Teleports Land One Step Away, Items Right On The Point
    public virtual float TeleportationOffset => 1;

    protected virtual bool ShouldFilter(MapFilter mapFilter)
    {
        // Only Places With Requirements Can Be Inaccessible
        return mapFilter.RequirementsFilter == MapFilter.RequirementsFilterType.InaccessibleOnly
            || ShouldFilterByPlace(mapFilter);
    }

    protected bool ShouldFilterByPlace(MapFilter mapFilter)
    {
        return (mapFilter.AreaFilter != AreaFilter.All && !mapFilter.AreaFilter.ShouldInclude(this))
            || (
                !string.IsNullOrWhiteSpace(mapFilter.NormalizedLowercaseFullText)
                && FullTextFilter(mapFilter.NormalizedLowercaseFullText)
            );
    }

    protected virtual bool FullTextFilter(string normalizedLowercaseFullText)
    {
        return !NormalizedLowercaseTitle.Contains(normalizedLowercaseFullText)
            && !(NormalizedLowercaseDescription?.Contains(normalizedLowercaseFullText) ?? false);
    }

    protected static string BaseIcon(string icon)
    {
        return $"images/icons/{icon}";
    }
}

public interface IPoi
{
    public Position? Position { get; }
    public string PrintableCoordinates { get; }
    public bool IsUnderground { get; }
    public string? AreaName { get; }
    public string Icon { get; }
    public float MapLeft { get; }
    public float MapTop { get; }
    public string Title { get; }
    public string? Description { get; }
    public bool Enabled { get; set; }
    public bool Visible { get; }
    public int IconSize { get; }
    public int IconZIndex { get; }
    public float IconRotation { get; }
    public bool IsSelected { get; set; }
    public void ApplyFilter(MapFilter mapFilter);
}
