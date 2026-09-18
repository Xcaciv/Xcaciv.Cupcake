namespace Xcaciv.Command.Packages.Models;

public class PackageSourceSettings
{
    public ConfigFile NugetConfig { get; set; } = new ConfigFile();

    // Install command settings
    public string PackageId { get; set; } = String.Empty;
    public string? Version { get; set; }
    public string? InstallRoot { get; set; }

    // Search command settings
    public string Terms { get; set; } = String.Empty;
    public int Take { get; set; } = 20;
    public bool IncludePrerelease { get; set; }
    public string Verbosity { get; set; } = "normal";
    public bool AllowExternalDependencies { get; set; }
}
