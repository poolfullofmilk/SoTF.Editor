using Newtonsoft.Json;
using SoTF.Editor.Data.Enums;

namespace SoTF.Editor.Data.Models;

public class Position(float x, float y, float z)
{
    private readonly float _y = y;

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
        return _y.Equals(other._y) && X.Equals(other.X) && Z.Equals(other.Z);
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
        return HashCode.Combine(_y, X, Z, Area);
    }

    public List<Tuple<float, float>> DistributeCoordinates(
        int count,
        int space,
        SpawnPattern spawnPattern
    )
    {
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
        var random = new Random();

        for (var index = 1; index < count; index++)
        {
            var angle = (float)(random.NextDouble() * 2 * Math.PI);
            var radius = (float)(random.NextDouble() * space);

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
        var newCoordinates = new List<Tuple<float, float>>();

        var side = (int)Math.Ceiling(Math.Sqrt(count / 4.0)) * 2;
        var totalPoints = side * 4 - 4;

        while (totalPoints < count)
        {
            side += 2;
            totalPoints = side * 4 - 4;
        }

        var excessPoints = totalPoints - count;
        var horizontalPoints = side - excessPoints / 2;
        var verticalPoints = side - 2 - (excessPoints - excessPoints / 2);

        var adjustedSpace = (count - 4) * space / (float)(4 * (side - 1) - 4);

        var startX = X - (horizontalPoints - 1) * adjustedSpace / 2f;
        var startZ = Z - (verticalPoints - 1) * adjustedSpace / 2f;

        // Top Row
        for (var index = 0; index < horizontalPoints; index++)
        {
            var newX = startX + index * adjustedSpace;
            newCoordinates.Add(Tuple.Create(newX, startZ));
        }

        // Right Column
        for (var index = 1; index < verticalPoints; index++)
        {
            var newX = startX + (horizontalPoints - 1) * adjustedSpace;
            var newY = startZ + index * adjustedSpace;
            newCoordinates.Add(Tuple.Create(newX, newY));
        }

        // Bottom Row
        for (var index = 0; index < horizontalPoints; index++)
        {
            var newX = startX + (horizontalPoints - 1 - index) * adjustedSpace;
            var newY = startZ + (verticalPoints - 1) * adjustedSpace;
            newCoordinates.Add(Tuple.Create(newX, newY));
        }

        // Left Column
        for (var index = 1; index < verticalPoints - 1; index++)
        {
            var newY = startZ + (verticalPoints - 1 - index) * adjustedSpace;
            newCoordinates.Add(Tuple.Create(startX, newY));
        }

        return newCoordinates;
    }

    private enum LineOrientation
    {
        Horizontal,
        Vertical,
    }
}
