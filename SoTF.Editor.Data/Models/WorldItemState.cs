using Newtonsoft.Json;
using SoTF.Editor.Data.Enums;

namespace SoTF.Editor.Data.Models;

public sealed record WorldItemState(
    string ObjectName,
    [property: JsonIgnore] string Group,
    Position Position,
    WorldItemType WorldItemType,
    bool IsRuntimeCreated
);
