namespace Xcaciv.Command.Packages.Services;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Xcaciv.Command.Packages.Validation;

public class InstallService : IInstallService
{
    private readonly InstallationRootResolver installationRootResolver;
    private readonly NuGetClientFactory clientFactory;
    private readonly InputValidator inputValidator;
    private readonly CommandValidationService commandValidator;
    private readonly SecurityPolicyService securityPolicy;

    public InstallService(
        InstallationRootResolver installationRootResolver,
        NuGetClientFactory clientFactory,
        InputValidator inputValidator,
        CommandValidationService commandValidator,
        SecurityPolicyService securityPolicy)
    {
        this.installationRootResolver = installationRootResolver ?? throw new ArgumentNullException(nameof(installationRootResolver));
        this.clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        this.inputValidator = inputValidator ?? throw new ArgumentNullException(nameof(inputValidator));
        this.commandValidator = commandValidator ?? throw new ArgumentNullException(nameof(commandValidator));
        this.securityPolicy = securityPolicy ?? throw new ArgumentNullException(nameof(securityPolicy));
    }

    public async Task<InstallResult> InstallAsync(
        string sourceUrl,
        string packageId,
        string? version,
        string? installRootOverride,
        CancellationToken cancellationToken)
    {
        this.inputValidator.ValidatePackageId(packageId);

        var installRoot = this.installationRootResolver.Resolve(installRootOverride);
        _ = new LocalCacheManager(installRoot);
        var packagesDir = Path.Combine(installRoot, "packages");
        Directory.CreateDirectory(packagesDir);

        var repository = this.clientFactory.GetRepository(sourceUrl);
        var metadataResource = await repository.GetResourceAsync<PackageMetadataResource>(cancellationToken);

        var targetVersion = !String.IsNullOrWhiteSpace(version)
            ? NuGetVersion.Parse(version)
            : null;

        var metadata = (await metadataResource.GetMetadataAsync(packageId, includePrerelease: true, includeUnlisted: false, this.clientFactory.CreateCacheContext(), this.clientFactory.CreateNuGetLogger(), cancellationToken))?.ToList() ?? new List<IPackageSearchMetadata>();

        if (metadata.Count == 0)
        {
            return InstallResult.Failure($"Package '{packageId}' not found in source");
        }

        var selectedVersion = targetVersion is not null
            ? metadata.FirstOrDefault(m => String.Equals(m.Identity.Version.ToNormalizedString(), targetVersion.ToNormalizedString(), StringComparison.OrdinalIgnoreCase))
            : metadata[0];

        if (selectedVersion is null)
        {
            return InstallResult.Failure($"Package '{packageId}' version '{version}' not found");
        }

        var packageDir = Path.Combine(packagesDir, packageId, selectedVersion.Identity.Version.ToNormalizedString());
        var backupDir = packageDir + ".backup";

        try
        {
            if (Directory.Exists(packageDir))
            {
                Directory.Move(packageDir, backupDir);
            }

            Directory.CreateDirectory(packageDir);

            var downloadResource = await repository.GetResourceAsync<DownloadResource>(cancellationToken).ConfigureAwait(false);
            var downloadContext = new PackageDownloadContext(this.clientFactory.CreateCacheContext());
            var downloadResult = await downloadResource.GetDownloadResourceResultAsync(
                selectedVersion.Identity,
                downloadContext,
                /* globalPackagesFolder: */ String.Empty,
                this.clientFactory.CreateNuGetLogger(),
                cancellationToken).ConfigureAwait(false);

            using (var packageStream = downloadResult.PackageStream)
            {
                using (var packageReader = new PackageArchiveReader(packageStream))
                {
                    var dependencies = (await packageReader.GetPackageDependenciesAsync(cancellationToken).ConfigureAwait(false))?.SelectMany(g => g.Packages).Select(p => p.Id).Distinct().ToList() ?? new List<string>();

                    this.securityPolicy.EnforcePolicyOnDependencies(dependencies);

                    await packageReader.CopyFilesAsync(
                        packageDir,
                        new[] { "lib/" },
                        (sourceFile, targetFile, _) => targetFile,
                        this.clientFactory.CreateNuGetLogger(),
                        cancellationToken).ConfigureAwait(false);
                }
            }

            if (!this.commandValidator.ValidateInstalledPackage(packageDir))
            {
                Directory.Delete(packageDir, recursive: true);

                if (Directory.Exists(backupDir))
                {
                    Directory.Move(backupDir, packageDir);
                }

                return InstallResult.Failure($"Package '{packageId}' failed validation (no ICommandDelegate implementations found)");
            }

            if (Directory.Exists(backupDir))
            {
                Directory.Delete(backupDir, recursive: true);
            }

            return InstallResult.Success($"Package '{packageId}' version '{selectedVersion.Identity.Version}' installed successfully");
        }
        catch (Exception ex)
        {
            if (Directory.Exists(packageDir))
            {
                Directory.Delete(packageDir, recursive: true);
            }

            if (Directory.Exists(backupDir))
            {
                Directory.Move(backupDir, packageDir);
            }

            return InstallResult.Failure($"Installation failed: {ex.Message}");
        }
    }
}

public sealed record InstallResult(bool IsSuccess, string Message)
{
    public static InstallResult Success(string message) => new(true, message);
    public static InstallResult Failure(string message) => new(false, message);
}
