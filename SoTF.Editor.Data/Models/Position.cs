using System.Diagnostics;
using Newtonsoft.Json;
using SoTF.Editor.Data.Enums;

namespace SoTF.Editor.Data.Models;

public class Position(float x, float y, float z)
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
    public string PrintableShort => $"X: {X:F2}, Y: {Y:F2}, Z: {Z:F2}";

    [JsonIgnore]
    public float Rotation { get; set; }

    public Position WithoutOffset()
    {
        return WithoutOffset(Area);
    }

    public Position WithoutOffset(Area area)
    {
        return new Position(X, _y, Z) { Area = area };
    }

    public Position WithYOffset(int yOffset)
    {
        var newPos = WithoutOffset();
        newPos.YOffset = yOffset;
        return newPos;
    }

    public bool HasSameCoordinates(Position other)
    {
        // The Coordinates The Save Holds, Offset Included
        return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
    }

    private bool Equals(Position other)
    {
        return HasSameCoordinates(other) && Area.Equals(other.Area);
    }

    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        return obj.GetType() == GetType() && Equals((Position)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y, Z, Area);
    }

    public List<Tuple<float, float>> DistributeCoordinates(
        int count,
        int space,
        SpawnPattern spawnPattern
    )
    {
        // No Space Means Everyone On The Spot
        if (space <= 0)
        {
            return [.. Enumerable.Repeat(Tuple.Create(X, Z), Math.Max(count, 1))];
        }

        return spawnPattern switch
        {
            SpawnPattern.Cross => DistributeCoordinatesInCross(count, space),
            SpawnPattern.VerticalLine => DistributeCoordinatesInLine(
                count,
                space,
                LineOrientation.Vertical
            ),
            SpawnPattern.HorizontalLine => DistributeCoordinatesInLine(
                count,
                space,
                LineOrientation.Horizontal
            ),
            SpawnPattern.Random => DistributeCoordinatesRandomly(count, space),
            SpawnPattern.Rectangle => DistributeCoordinatesInRectangle(count, space),
            _ => DistributeCoordinatesInGrid(count, space),
        };
    }

    private List<Tuple<float, float>> DistributeCoordinatesInGrid(int count, int space)
    {
        var newCoordinates = new List<Tuple<float, float>> { Tuple.Create(X, Z) };

        var currentCount = 1;
        var currentLevel = 0;
        var currentDirection = 0;

        float[] directionX = [1, 0, -1, 0];
        float[] directionY = [0, -1, 0, 1];

        var curX = X;
        var curZ = Z;

        while (currentCount < count)
        {
            currentLevel++;
            for (var index = 0; index < 2; index++)
            {
                for (
                    var innerIndex = 0;
                    innerIndex < currentLevel && currentCount < count;
                    innerIndex++
                )
                {
                    curX += directionX[currentDirection] * space;
                    curZ += directionY[currentDirection] * space;

                    newCoordinates.Add(Tuple.Create(curX, curZ));
                    currentCount++;
                }

                currentDirection = (currentDirection + 1) % 4;
            }
        }

        return newCoordinates;
    }

    private List<Tuple<float, float>> DistributeCoordinatesRandomly(int count, int space)
    {
        var newCoordinates = new List<Tuple<float, float>> { Tuple.Create(X, Z) };

        for (var index = 1; index < count; index++)
        {
            var angle = (float)(Random.Shared.NextDouble() * 2 * Math.PI);
            var radius = (float)(Random.Shared.NextDouble() * space);

            var newX = X + radius * (float)Math.Cos(angle);
            var newY = Z + radius * (float)Math.Sin(angle);

            newCoordinates.Add(Tuple.Create(newX, newY));
        }

        return newCoordinates;
    }

    private List<Tuple<float, float>> DistributeCoordinatesInLine(
        int count,
        int space,
        LineOrientation orientation
    )
    {
        var newCoordinates = new List<Tuple<float, float>> { Tuple.Create(X, Z) };

        var halfCount = count / 2;

        for (var index = 1; index <= halfCount; index++)
        {
            float newX,
                newY;

            if (orientation == LineOrientation.Horizontal)
            {
                newX = X + index * space;
                newY = Z;
                newCoordinates.Add(Tuple.Create(newX, newY));

                newX = X - index * space;
            }
            else
            {
                newX = X;
                newY = Z + index * space;
                newCoordinates.Add(Tuple.Create(newX, newY));

                newY = Z - index * space;
            }

            newCoordinates.Add(Tuple.Create(newX, newY));
        }

        if (count % 2 != 0)
        {
            float newX,
                newY;

            if (orientation == LineOrientation.Horizontal)
            {
                newX = X + (halfCount + 1) * space;
                newY = Z;
            }
            else
            {
                newX = X;
                newY = Z + (halfCount + 1) * space;
            }

            newCoordinates.Add(Tuple.Create(newX, newY));
        }

        return newCoordinates;
    }

    private List<Tuple<float, float>> DistributeCoordinatesInCross(int count, int space)
    {
        var newCoordinates = new List<Tuple<float, float>> { Tuple.Create(X, Z) };
        var halfCount = count / 4;

        for (var index = 1; index <= halfCount; index++)
        {
            // Right
            var newX = X + index * space;
            var newY = Z;
            newCoordinates.Add(Tuple.Create(newX, newY));

            // Left
            newX = X - index * space;
            newCoordinates.Add(Tuple.Create(newX, newY));

            // Top
            newX = X;
            newY = Z + index * space;
            newCoordinates.Add(Tuple.Create(newX, newY));

            // Bottom
            newY = Z - index * space;
            newCoordinates.Add(Tuple.Create(newX, newY));
        }

        if (count % 4 != 0)
        {
            var extraCount = count % 4;

            for (var index = 0; index < extraCount; index++)
            {
                float newX,
                    newY;

                switch (index)
                {
                    case 0:
                        newX = X + (halfCount + 1) * space;
                        newY = Z;
                        break;
                    case 1:
                        newX = X - (halfCount + 1) * space;
                        newY = Z;
                        break;
                    default:
                        newX = X;
                        newY = Z + (halfCount + 1) * space;
                        break;
                }

                newCoordinates.Add(Tuple.Create(newX, newY));
            }
        }

        return newCoordinates;
    }

    private List<Tuple<float, float>> DistributeCoordinatesInRectangle(int count, int space)
    {
        // Points Walk The Edge Of A Square, Evenly Spaced
        var side = count * space / 4f;
        var left = X - side / 2;
        var top = Z - side / 2;
        var newCoordinates = new List<Tuple<float, float>>();

        for (var index = 0; index < count; index++)
        {
            var distance = index * (float)space;
            var edge = (int)(distance / side);
            var along = distance - edge * side;
            newCoordinates.Add(
                edge switch
                {
                    0 => Tuple.Create(left + along, top),
                    1 => Tuple.Create(left + side, top + along),
                    2 => Tuple.Create(left + side - along, top + side),
                    _ => Tuple.Create(left, top + side - along),
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
                    !float.IsFinite(coordinate.Item1) || !float.IsFinite(coordinate.Item2)
                )
            )
            {
                return false;
            }
        }

        return true;
    }

    private enum LineOrientation
    {
        Horizontal,
        Vertical,
    }
}
