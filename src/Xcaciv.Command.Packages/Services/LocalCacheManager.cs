using System;
using System.IO;
using System.Security.Cryptography;

namespace Xcaciv.Command.Packages.Services;

public class LocalCacheManager
{
    private readonly string cacheRoot;

    public LocalCacheManager(string installationRoot)
    {
        if (String.IsNullOrWhiteSpace(installationRoot))
        {
            throw new ArgumentException("Installation root is required", nameof(installationRoot));
        }

        this.cacheRoot = Path.Combine(installationRoot, "cache");
        Directory.CreateDirectory(this.cacheRoot);
    }

    public string GetPackageCachePath(string packageId, string version)
    {
        if (String.IsNullOrWhiteSpace(packageId))
        {
            throw new ArgumentException("Package id is required", nameof(packageId));
        }

        if (String.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentException("Version is required", nameof(version));
        }

        var safeId = SanitizeSegment(packageId);
        var safeVersion = SanitizeSegment(version);
        return Path.Combine(this.cacheRoot, safeId, safeVersion, "package.nupkg");
    }

    public bool TryGetValidCache(string packageId, string version, string expectedChecksum, out string cachedPath)
    {
        cachedPath = GetPackageCachePath(packageId, version);
        if (!File.Exists(cachedPath))
        {
            return false;
        }

        if (String.IsNullOrWhiteSpace(expectedChecksum))
        {
            return false;
        }

        var actual = ComputeSha256(cachedPath);
        return String.Equals(actual, expectedChecksum, StringComparison.OrdinalIgnoreCase);
    }

    public void WriteCache(string packageId, string version, Stream packageStream, string expectedChecksum)
    {
        var path = GetPackageCachePath(packageId, version);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var tempPath = path + ".tmp";
        try
        {
            using (var file = File.Create(tempPath))
            {
                packageStream.CopyTo(file);
            }

            if (!String.IsNullOrWhiteSpace(expectedChecksum))
            {
                var actual = ComputeSha256(tempPath);
                if (!String.Equals(actual, expectedChecksum, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Checksum verification failed for cached package.");
                }
            }

            File.Move(tempPath, path, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
            throw;
        }
    }

    private static string SanitizeSegment(string segment)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            segment = segment.Replace(invalid, '-');
        }

        return segment;
    }

    private static string ComputeSha256(string path)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(path);
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }
}