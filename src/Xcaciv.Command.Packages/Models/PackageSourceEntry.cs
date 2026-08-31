using System;
using System.Text.Json.Serialization;

namespace Xcaciv.Command.Packages.Models;

public class PackageSourceEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = String.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = String.Empty;

    [JsonPropertyName("isDefault")]
    public bool IsDefault { get; set; }

    [JsonPropertyName("precedence")]
    public Int32 Precedence { get; set; }

    [JsonPropertyName("credentialKey")]
    public string CredentialKey { get; set; } = String.Empty;
}
