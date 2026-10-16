using SoTF.Editor.Data.Helpers;

namespace SoTF.Editor.Data.Models.Map;

public abstract class BasePoi : IPoi
{
    private Position? _position;

    protected BasePoi(Position position)
    {
        _position = position;
        X = position.X;
        Z = position.Z;
        (MapLeft, MapTop) = CoordinateConverter.IngameToPixel(position.X, position.Z);
    }

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

    private string NormalizedLowercaseTitle
    {
        get { return field ??= TranslationHelper.Normalize(Title).ToLower(); }
    }

    private string? NormalizedLowercaseDescription
    {
        get
        {
            if (field != null)
            {
                return field;
            }

            if (Description != null)
            {
                field = TranslationHelper.Normalize(Description).ToLower();
            }

            return field;
        }
    }

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
    public virtual int IconZIndex => IsSelected ? 99 : 0;
    public float IconRotation => Position?.Rotation ?? 0;

    public float MapLeft { get; private set; }

    public float MapTop { get; private set; }

    public virtual void ApplyFilter(MapFilter mapFilter)
    {
        Filtered = ShouldFilter(mapFilter);
    }

    public void SetEnabledNoRefresh(bool value)
    {
        Enabled = value;
    }

    public virtual string Title => "";

    public string? Description { get; init; }

    public virtual void GetTeleportationOffset(
        out float xOffset,
        out float yOffset,
        out float zOffset
    )
    {
        xOffset = 1;
        yOffset = 1;
        zOffset = 1;
    }

    protected virtual bool ShouldFilter(MapFilter mapFilter)
    {
        return ShouldFilterByArea(mapFilter) || ShouldFilterByFullText(mapFilter);
    }

    private bool ShouldFilterByArea(MapFilter mapFilter)
    {
        if (mapFilter.AreaFilter != AreaFilter.All)
        {
            return !mapFilter.AreaFilter.ShouldInclude(this);
        }

        return false;
    }

    private bool ShouldFilterByFullText(MapFilter mapFilter)
    {
        return !string.IsNullOrWhiteSpace(mapFilter.NormalizedLowercaseFullText)
            && FullTextFilter(mapFilter.NormalizedLowercaseFullText);
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

    public bool CanTeleportPlayer()
    {
        return Position != null && this is not PlayerPoi;
    }

    public bool CanTeleportKelvin()
    {
        return Position != null && !IsActorOfType(Constants.Actors.KelvinTypeId);
    }

    public bool CanTeleportVirginia()
    {
        return Position != null && !IsActorOfType(Constants.Actors.VirginiaTypeId);
    }

    private bool IsActorOfType(int typeId)
    {
        return this is ActorPoi actorPoi && actorPoi.Actor.TypeId == typeId;
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
    void SetEnabledNoRefresh(bool value);
}
