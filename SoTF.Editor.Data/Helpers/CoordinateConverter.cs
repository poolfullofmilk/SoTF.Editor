namespace SoTF.Editor.Data.Helpers;

public static class CoordinateConverter
{
    private static readonly float s_ingameToPixelOffsetX;
    private static readonly float s_ingameToPixelOffsetY;
    private static readonly float s_ingameToPixelScaleX;
    private static readonly float s_ingameToPixelScaleY;

    static CoordinateConverter()
    {
        // Pixel Coordinates And Their In Game Coordinates
        float[] pixelXs =
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
        float[] pixelYs =
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
        float[] ingameXs =
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
        float[] ingameYs =
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

        // Inverse Scale And Offset For The X Axis
        var inverseScaleXandOffsetX = CalculateScaleAndOffset(ingameXs, pixelXs);
        s_ingameToPixelScaleX = inverseScaleXandOffsetX.Item1;
        s_ingameToPixelOffsetX = inverseScaleXandOffsetX.Item2;

        // Inverse Scale And Offset For The Y Axis
        var inverseScaleYandOffsetY = CalculateScaleAndOffset(ingameYs, pixelYs);
        s_ingameToPixelScaleY = inverseScaleYandOffsetY.Item1;
        s_ingameToPixelOffsetY = inverseScaleYandOffsetY.Item2;
    }

    private static Tuple<float, float> CalculateScaleAndOffset(float[] fromValues, float[] toValues)
    {
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
        var offset = (sumY - scale * sumX) / count;

        return new Tuple<float, float>(scale, offset);
    }

    public static (float, float) IngameToPixel(float ingameX, float ingameY)
    {
        var pixelX = s_ingameToPixelScaleX * ingameX + s_ingameToPixelOffsetX;
        var pixelY = s_ingameToPixelScaleY * ingameY + s_ingameToPixelOffsetY;
        return (pixelX, pixelY);
    }
}
