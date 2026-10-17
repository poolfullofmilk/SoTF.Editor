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
            DataType.ReadOnly => StringValue,
            DataType.String => StringValue,
            DataType.Boolean => BoolValue,
            DataType.Integer => IntValue,
            DataType.Decimal => DecimalValue,
            DataType.Enum => SelectedItem,
            _ => throw new ArgumentOutOfRangeException(),
        };
    }
}
