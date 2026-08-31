using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Xcaciv.Command.Packages.Models;

public class ConfigFile
{
    [JsonPropertyName("sources")]
    public List<PackageSourceEntry> Sources { get; set; } = new();

    [JsonPropertyName("defaultSource")]
    public string DefaultSource { get; set; } = "https://api.nuget.org/v3/index.json";

    [JsonPropertyName("allowExternalDependencies")]
    public bool AllowExternalDependencies { get; set; }
}
