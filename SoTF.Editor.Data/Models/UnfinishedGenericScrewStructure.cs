using Newtonsoft.Json.Linq;

namespace SoTF.Editor.Data.Models;

public record UnfinishedGenericScrewStructure(int Id, Position Pos, JToken Rot, int Added);
