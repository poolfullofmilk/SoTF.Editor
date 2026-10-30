using System.Diagnostics;
using Newtonsoft.Json;
using SoTF.Editor.Data.Enums;
using SoTF.Editor.Data.Helpers;

namespace SoTF.Editor.Data.Models;

public sealed class Position(float x, float y, float z)
{
    private readonly float _y = y;

    static Position()
    {
        Debug.Assert(SelfTestPasses());
    }

    [JsonIgnore]
    public Area Area { get; init; } = AreaMaskManager.Surface;

    private float YOffset { get; set; }

    [JsonProperty("x")]
    public float X { get; init; } = x;

    [JsonProperty("y")]
    public float Y => _y + YOffset;

    [JsonProperty("z")]
    public float Z { get; init; } = z;

    [JsonIgnore]
    public string PrintableShort => Formatter.FormatPosition(X, Y, Z);

    public float GroundDistanceTo(Position other)
    {
        // How Far Someone Walks, Heights Left Out
        var (x, z) = (X - other.X, Z - other.Z);
        return MathF.Sqrt(x * x + z * z);
    }

    public Position WithoutOffset()
    {
        return WithoutOffset(Area);
    }

    public Position WithoutOffset(Area area)
    {
        return new Position(X, _y, Z) { Area = area };
    }

    public Position ForTeleport()
    {
        // Lands A Little Above, Never Inside The Ground
        var newPos = WithoutOffset();
        newPos.YOffset = 3;
        return newPos;
    }

    public bool HasSameCoordinates(Position other)
    {
        // The Coordinates The Save Holds, Offset Included
        return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
    }

    public override bool Equals(object? obj)
    {
        return obj is Position other && HasSameCoordinates(other) && Area.Equals(other.Area);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y, Z, Area);
    }

    public List<(float X, float Z)> DistributeCoordinates(
        int count,
        int space,
        SpawnPattern spawnPattern
    )
    {
        // No Space Means Everyone On The Spot
        if (space <= 0)
        {
            return [.. Enumerable.Repeat((X, Z), Math.Max(count, 1))];
        }

        return spawnPattern switch
        {
            SpawnPattern.Cross => DistributeCoordinatesInCross(count, space),
            SpawnPattern.VerticalLine => DistributeCoordinatesInLine(count, space, false),
            SpawnPattern.HorizontalLine => DistributeCoordinatesInLine(count, space, true),
            SpawnPattern.Random => DistributeCoordinatesRandomly(count, space),
            SpawnPattern.Rectangle => DistributeCoordinatesInRectangle(count, space),
            _ => DistributeCoordinatesInGrid(count, space),
        };
    }

    private List<(float X, float Z)> DistributeCoordinatesInGrid(int count, int space)
    {
        // A Square Spiral Outwards From The Center
        List<(float X, float Z)> newCoordinates = [(X, Z)];
        float[] directionX = [1, 0, -1, 0];
        float[] directionZ = [0, -1, 0, 1];
        var currentX = X;
        var currentZ = Z;
        var currentDirection = 0;

        for (var level = 1; newCoordinates.Count < count; level++)
        {
            for (var side = 0; side < 2; side++)
            {
                for (var step = 0; step < level && newCoordinates.Count < count; step++)
                {
                    currentX += directionX[currentDirection] * space;
                    currentZ += directionZ[currentDirection] * space;
                    newCoordinates.Add((currentX, currentZ));
                }

                currentDirection = (currentDirection + 1) % 4;
            }
        }

        return newCoordinates;
    }

    private List<(float X, float Z)> DistributeCoordinatesRandomly(int count, int space)
    {
        List<(float X, float Z)> newCoordinates = [(X, Z)];
        for (var index = 1; index < count; index++)
        {
            var angle = (float)(Random.Shared.NextDouble() * 2 * Math.PI);
            var radius = (float)(Random.Shared.NextDouble() * space);
            newCoordinates.Add(
                (X + radius * (float)Math.Cos(angle), Z + radius * (float)Math.Sin(angle))
            );
        }

        return newCoordinates;
    }

    private List<(float X, float Z)> DistributeCoordinatesInLine(
        int count,
        int space,
        bool horizontal
    )
    {
        // The Center, Then One Step Further Each Side In Turn
        return
        [
            (X, Z),
            .. Enumerable
                .Range(0, count)
                .Select(index =>
                {
                    var offset = (index / 2 + 1) * space * (index % 2 == 0 ? 1 : -1);
                    return horizontal ? (X + offset, Z) : (X, Z + offset);
                }),
        ];
    }

    private List<(float X, float Z)> DistributeCoordinatesInCross(int count, int space)
    {
        // The Center, Then Right, Left, Up, Down, Ring By Ring
        return
        [
            (X, Z),
            .. Enumerable
                .Range(0, count)
                .Select(index =>
                {
                    var reach = (index / 4 + 1) * space;
                    return (index % 4) switch
                    {
                        0 => (X + reach, Z),
                        1 => (X - reach, Z),
                        2 => (X, Z + reach),
                        _ => (X, Z - reach),
                    };
                }),
        ];
    }

    private List<(float X, float Z)> DistributeCoordinatesInRectangle(int count, int space)
    {
        // Points Walk The Edge Of A Square, Evenly Spaced
        var side = count * space / 4f;
        var left = X - side / 2;
        var top = Z - side / 2;
        List<(float X, float Z)> newCoordinates = [];

        for (var index = 0; index < count; index++)
        {
            var distance = index * (float)space;
            var edge = (int)(distance / side);
            var along = distance - edge * side;
            newCoordinates.Add(
                edge switch
                {
                    0 => (left + along, top),
                    1 => (left + side, top + along),
                    2 => (left + side - along, top + side),
                    _ => (left, top + side - along),
                }
            );
        }

        return newCoordinates;
    }

    private static bool SelfTestPasses()
    {
        // Every Pattern Places Enough Finite Points
        var center = new Position(10, 0, 20);
        foreach (var pattern in Enum.GetValues<SpawnPattern>())
        foreach (var count in new[] { 1, 2, 3, 4, 5, 9, 17 })
        {
            var coordinates = center.DistributeCoordinates(count, 2, pattern);
            if (
                coordinates.Count < count
                || coordinates.Any(coordinate =>
                    !float.IsFinite(coordinate.X) || !float.IsFinite(coordinate.Z)
                )
            )
            {
                return false;
            }
        }

        // Lines And Crosses Keep Their Order, Center First
        return center
                .DistributeCoordinates(3, 2, SpawnPattern.HorizontalLine)
                .SequenceEqual([(10, 20), (12, 20), (8, 20), (14, 20)])
            && center
                .DistributeCoordinates(5, 2, SpawnPattern.Cross)
                .SequenceEqual([(10, 20), (12, 20), (8, 20), (10, 22), (10, 18), (14, 20)]);
    }
}
