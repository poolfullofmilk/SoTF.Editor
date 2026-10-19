using System.Globalization;
using System.Reflection;
using SoTF.Editor.Data.Models;

namespace SoTF.Editor.Data.Helpers;

public sealed class LoadedValues
{
    private readonly Dictionary<string, string> _values = [];

    public void Clear()
    {
        _values.Clear();
    }

    public void Remember(string key, object? value)
    {
        _values[key] = Format(value);
    }

    public void RememberProperties(string scope, object model)
    {
        foreach (var property in EditableProperties(model.GetType()))
        {
            Remember($"{scope}.{property.Name}", property.GetValue(model));
        }
    }

    public bool IsChanged(string key, object? value)
    {
        // Never Remembered Means Added Since The Load
        return !_values.TryGetValue(key, out var loaded) || loaded != Format(value);
    }

    public bool IsPropertyChanged(string scope, object model, string propertyName)
    {
        return IsChanged(
            $"{scope}.{propertyName}",
            model.GetType().GetProperty(propertyName)?.GetValue(model)
        );
    }

    public bool AnyPropertyChanged(string scope, object model)
    {
        return EditableProperties(model.GetType())
            .Any(property => IsChanged($"{scope}.{property.Name}", property.GetValue(model)));
    }

    public IEnumerable<string> KeysStartingWith(string prefix)
    {
        return _values.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static IEnumerable<PropertyInfo> EditableProperties(Type type)
    {
        // Settable Values Only, Computed Labels Follow The Language
        return type.GetProperties()
            .Where(property =>
                property.CanRead
                && property.CanWrite
                && property.GetIndexParameters().Length == 0
                && IsValue(property.PropertyType)
            );
    }

    private static bool IsValue(Type type)
    {
        return type.IsValueType
            || type == typeof(string)
            || type == typeof(Item)
            || type == typeof(Outfit)
            || type == typeof(Position);
    }

    public static string Format(object? value)
    {
        return value switch
        {
            null => "",
            Item item => $"Item {item.Id}",
            Outfit outfit => $"Outfit {outfit.Id}",
            Position position => string.Create(
                CultureInfo.InvariantCulture,
                $"{position.X} {position.Y} {position.Z} {position.Area.AreaMask}"
            ),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "",
        };
    }
}
