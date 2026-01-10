namespace Xcaciv.Command.Packages.Services;

using System;
using System.IO;
using System.Security.Cryptography;

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

        // If checksum validation is required, verify before writing
        if (!String.IsNullOrWhiteSpace(expectedChecksum))
        {
            // Read stream into memory to compute checksum
            var buffer = new byte[packageStream.Length];
            var bytesRead = packageStream.Read(buffer, 0, buffer.Length);
            packageStream.Position = 0;

            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var hash = sha.ComputeHash(buffer, 0, bytesRead);
                var actual = Convert.ToHexString(hash);

                if (!String.Equals(actual, expectedChecksum, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Checksum verification failed for cached package.");
                }
            }

            packageStream.Position = 0;
        }

        using var file = File.Create(path);
        packageStream.CopyTo(file);
        file.Flush(true);
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