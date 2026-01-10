namespace Xcaciv.Command.Packages.Services;

using System;
using System.Threading;
using System.Threading.Tasks;

public interface IInstallService
{
    Task<InstallResult> InstallAsync(string sourceUrl, string packageId, string? version, string? installRootOverride, CancellationToken cancellationToken);
}
