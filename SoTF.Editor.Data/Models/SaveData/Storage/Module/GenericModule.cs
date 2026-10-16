using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SoTF.Editor.Data.Models.SaveData.Storage.Module;

[JsonConverter(typeof(GenericStorageModuleConverter))]
public record GenericModule(int ModuleId, JObject ModuleToken) : BaseStorageModule(ModuleId)
{
    static GenericModule()
    {
        Debug.Assert(SelfTestPasses());
    }

    public virtual bool Equals(GenericModule? other)
    {
        // Tokens Compare By Content, Not By Reference
        return other != null
            && ModuleId == other.ModuleId
            && JToken.DeepEquals(ModuleToken, other.ModuleToken);
    }

    public override int GetHashCode()
    {
        return ModuleId;
    }

    private static bool SelfTestPasses()
    {
        var module = new GenericModule(6, JObject.Parse("{\"ModuleId\":6,\"VariantNumber\":0}"));
        return module.Equals(
                new GenericModule(6, JObject.Parse("{\"ModuleId\":6,\"VariantNumber\":0}"))
            )
            && !module.Equals(
                new GenericModule(6, JObject.Parse("{\"ModuleId\":6,\"VariantNumber\":1}"))
            );
    }
}
