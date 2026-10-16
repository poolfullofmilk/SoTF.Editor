using Newtonsoft.Json;

namespace SoTF.Editor.Data.Models.SaveData;

public abstract record SotfBaseModel(
    [property: JsonProperty(Order = -99999)] string Version = "0.0.0"
);
