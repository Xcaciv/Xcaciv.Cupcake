using System;
using System.IO;

namespace Xcaciv.Command.Packages.Services;

public class InstallationRootResolver
{
    private readonly string hostWorkingDirectory;

    public InstallationRootResolver(string hostWorkingDirectory)
    {
        this.hostWorkingDirectory = hostWorkingDirectory ?? throw new ArgumentNullException(nameof(hostWorkingDirectory));
    }

    public string Resolve(string? overridePath)
    {
        var basePath = String.IsNullOrWhiteSpace(overridePath)
            ? Path.Combine(this.hostWorkingDirectory, ".nuget")
            : Path.GetFullPath(overridePath);

        var canonicalBase = Path.GetFullPath(basePath);

        ValidateNotGlobalNuGetPath(canonicalBase);
        ValidateWritable(canonicalBase);

        return canonicalBase;
    }

    private static void ValidateNotGlobalNuGetPath(string path)
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (String.IsNullOrWhiteSpace(userProfile))
        {
            return;
        }

        var userPackages = Path.Combine(userProfile, ".nuget", "packages");
        if (path.StartsWith(userPackages, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Install root cannot be global/user NuGet packages path: {path}");
        }
    }

    private static void ValidateWritable(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        var probeFile = Path.Combine(path, $".writecheck_{Guid.NewGuid():N}");
        try
        {
            File.WriteAllText(probeFile, "probe");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Install root is not writable: {path}", ex);
        }
        finally
        {
            if (File.Exists(probeFile))
            {
                File.Delete(probeFile);
            }
        }
    }
}