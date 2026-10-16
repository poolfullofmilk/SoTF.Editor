using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json.Linq;

namespace SoTF.Editor.Data.Models.SaveData;

public record ActorItemBlock(int ItemId, int TotalCount, List<JToken> UniqueItems);
