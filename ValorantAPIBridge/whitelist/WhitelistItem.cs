using System.Text.Json.Serialization;

namespace ValorantAPIBridge.whitelist;

public class WhitelistItem
{
    [JsonPropertyName("origin")] public string Origin { get; set; }
    [JsonPropertyName("added")] public long Added { get; set; }
    [JsonPropertyName("lastUsed")] public long LastUsed { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
}