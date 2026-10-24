using Newtonsoft.Json.Linq;

namespace SoTF.Editor.Data.Models;

public class GenericSetting(string name, GenericSetting.DataType type, string? dataPath = null)
{
    public enum DataType
    {
        ReadOnly,
        String,
        Boolean,
        Integer,
        Decimal,
        Enum,
    }

    public bool? BoolValue { get; set; }

    public int? IntValue { get; set; } = 0;

    // A Double Writes Back The Exact Number It Read
    public double? DecimalValue { get; set; }

    public object? SelectedItem { get; set; }

    public string? StringValue { get; set; }

    public string Name { get; } = name;
    public string? DataPath { get; } = dataPath;
    public DataType Type { get; } = type;

    public Dictionary<object, string> PossibleValues { get; } = [];
    public int MinInt { get; init; }
    public int MaxInt { get; init; } = 1;

    public object? GetValue()
    {
        return Type switch
        {
            DataType.ReadOnly or DataType.String => StringValue,
            DataType.Boolean => BoolValue,
            DataType.Integer => IntValue,
            DataType.Decimal => DecimalValue,
            DataType.Enum => SelectedItem,
            _ => throw new ArgumentOutOfRangeException(),
        };
    }

    public bool MergeTo(JToken target)
    {
        if (Type == DataType.ReadOnly || DataPath == null || target is not JObject targetObject)
        {
            return false;
        }

        var newToken = GetValue() is { } value ? JToken.FromObject(value) : JValue.CreateNull();

        // The Game Leaves Out Defaults, Add Only Other Values
        if (targetObject[DataPath] is not { } token)
        {
            if (IsDefault(newToken))
            {
                return false;
            }

            targetObject[DataPath] = newToken;
            return true;
        }

        if (JToken.DeepEquals(newToken, token))
        {
            return false;
        }

        token.Replace(newToken);
        return true;
    }

    private static bool IsDefault(JToken token)
    {
        return token.Type switch
        {
            JTokenType.Null => true,
            JTokenType.Boolean => !token.Value<bool>(),
            JTokenType.Integer => token.Value<long>() == 0,
            JTokenType.Float => token.Value<double>() == 0,
            JTokenType.String => token.Value<string>() == "",
            _ => false,
        };
    }
}
