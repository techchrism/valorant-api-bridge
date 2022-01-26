using System.Text.Json.Serialization;

namespace ValorantAPIBridge.whitelist;

public class WhitelistRequest
{
    [JsonPropertyName("origin")] public string Origin { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
}