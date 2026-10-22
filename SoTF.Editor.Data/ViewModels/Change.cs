using SoTF.Editor.Data.Enums;

namespace SoTF.Editor.Data.ViewModels;

public sealed record Change(string Name, ChangeKind Kind = ChangeKind.Changed);
