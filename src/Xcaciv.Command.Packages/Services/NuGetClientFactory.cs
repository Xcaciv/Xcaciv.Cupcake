using System;
using Microsoft.Extensions.Logging;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using Xcaciv.Command.Packages.Abstractions;

namespace Xcaciv.Command.Packages.Services;

public class NuGetClientFactory
{
    private readonly ILogger<NuGetClientFactory> logger;

    public NuGetClientFactory(ILogger<NuGetClientFactory> logger)
    {
        this.logger = logger is null
            ? Microsoft.Extensions.Logging.Abstractions.NullLogger<NuGetClientFactory>.Instance
            : logger;
    }

    public SourceRepository GetRepository(string sourceUrl)
    {
        if (String.IsNullOrWhiteSpace(sourceUrl))
        {
            throw new ArgumentException("Source URL is required", nameof(sourceUrl));
        }

        if (!sourceUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only HTTPS sources are allowed.");
        }

        var packageSource = new PackageSource(sourceUrl)
        {
            ProtocolVersion = 3
        };

        return Repository.Factory.GetCoreV3(packageSource.Source);
    }

    public SourceCacheContext CreateCacheContext() => new();

    public NuGet.Common.ILogger CreateNuGetLogger() => new NuGetLoggerAdapter(this.logger);
}
