using System.Collections.Concurrent;
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
        return ChangedProperties(scope, model).Any();
    }

    public IEnumerable<string> ChangedProperties(string scope, object model)
    {
        return EditableProperties(model.GetType())
            .Where(property => IsChanged($"{scope}.{property.Name}", property.GetValue(model)))
            .Select(property => property.Name);
    }

    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> s_editableProperties = [];

    private static PropertyInfo[] EditableProperties(Type type)
    {
        // Settable Values Only, Computed Labels Follow The Language
        return s_editableProperties.GetOrAdd(
            type,
            type =>
                [
                    .. type.GetProperties()
                        .Where(property =>
                            property.CanRead
                            && property.CanWrite
                            && property.GetIndexParameters().Length == 0
                            && IsValue(property.PropertyType)
                        ),
                ]
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
