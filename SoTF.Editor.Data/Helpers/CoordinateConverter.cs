namespace SoTF.Editor.Data.Helpers;

public static class CoordinateConverter
{
    // Pixel Coordinates And Their In Game Coordinates
    private static readonly float[] s_pixelXs =
    [
        1311,
        1592.5f,
        1426,
        1037,
        688,
        772.3f,
        709.0f,
        786.2f,
        789.8f,
        899.5f,
        1259.8f,
    ];
    private static readonly float[] s_pixelYs =
    [
        2064,
        2155,
        1494.5f,
        1425,
        1407,
        1486.3f,
        1713.7f,
        1867.3f,
        2020.1f,
        2125.4f,
        1822.8f,
    ];
    private static readonly float[] s_ingameXs =
    [
        -719.0499f,
        -444.96793f,
        -604.09875f,
        -989.2298f,
        -1328.8882f,
        -1245.4705f,
        -1308.0143f,
        -1232.5912f,
        -1226.8536f,
        -1112.021f,
        -770.1515f,
    ];
    private static readonly float[] s_ingameYs =
    [
        -17.139717f,
        -103.88483f,
        541.12946f,
        611.64136f,
        625.7892f,
        549.34155f,
        326.0083f,
        176.03516f,
        27.191362f,
        -74.49177f,
        219.75914f,
    ];

    private static readonly (float Scale, float Offset) s_xAxis = Fit(s_ingameXs, s_pixelXs);
    private static readonly (float Scale, float Offset) s_yAxis = Fit(s_ingameYs, s_pixelYs);

    private static (float Scale, float Offset) Fit(float[] fromValues, float[] toValues)
    {
        // Least Squares Line Through The Known Points
        var count = fromValues.Length;
        float sumX = 0,
            sumY = 0,
            sumXy = 0,
            sumXx = 0;

        for (var index = 0; index < count; index++)
        {
            sumX += fromValues[index];
            sumY += toValues[index];
            sumXy += fromValues[index] * toValues[index];
            sumXx += fromValues[index] * fromValues[index];
        }

        var scale = (count * sumXy - sumX * sumY) / (count * sumXx - sumX * sumX);
        return (scale, (sumY - scale * sumX) / count);
    }

    public static (float Left, float Top) IngameToPixel(float ingameX, float ingameY)
    {
        return (s_xAxis.Scale * ingameX + s_xAxis.Offset, s_yAxis.Scale * ingameY + s_yAxis.Offset);
    }

    public static (float X, float Z) PixelToIngame(float left, float top)
    {
        return ((left - s_xAxis.Offset) / s_xAxis.Scale, (top - s_yAxis.Offset) / s_yAxis.Scale);
    }
}
