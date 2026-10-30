using Newtonsoft.Json.Linq;

namespace SoTF.Editor.Data.Models.SaveData;

public sealed record ActorItemBlock(int ItemId, int TotalCount, List<JToken> UniqueItems);
