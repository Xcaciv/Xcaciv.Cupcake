namespace Xcaciv.Command.Packages.Commands;

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Xcaciv.Command.Core;
using Xcaciv.Command.Interface;
using Xcaciv.Command.Interface.Attributes;
using Xcaciv.Command.Interface.Parameters;
using Xcaciv.Command.Packages.Services;
using Xcaciv.Command.Packages.Validation;

[CommandRegister("install", "Install a command package from a NuGet source")]
[CommandRoot("package", "Manage command packages")]
[CommandParameterOrdered("packageId", "Package identifier")]
[CommandParameterNamed("version", "Specific version to install (optional; latest used if omitted)")]
[CommandParameterNamed("source", "Package source URL (HTTPS)")]
[CommandParameterNamed("installRoot", "Override installation root directory")]
public class PackageInstallCommand : AbstractCommand
{
    private readonly IInstallService installService;
    private readonly IPackageSourceConfigService configService;

    public PackageInstallCommand()
        : this(CreateInstallService(), CreateConfigService())
    {
    }

    public PackageInstallCommand(IInstallService installService, IPackageSourceConfigService configService)
    {
        this.installService = installService ?? throw new ArgumentNullException(nameof(installService));
        this.configService = configService ?? throw new ArgumentNullException(nameof(configService));
    }

    public override IResult<string> HandleExecution(Dictionary<string, IParameterValue> parameters, IEnvironmentContext env)
    {
        var settings = this.configService.ResolveSettings(env, parameters);
        
        try
        {
            var result = this.installService.InstallAsync(
                settings.NugetConfig.DefaultSource,
                settings.PackageId,
                settings.Version,
                settings.InstallRoot,
                CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();

            if (result.IsSuccess)
            {
                return CommandResult<string>.Success(result.Message);
            }
            else
            {
                return CommandResult<string>.Failure(result.Message);
            }
        }
        catch (ArgumentException ex)
        {
            return CommandResult<string>.Failure($"Invalid input: {ex.Message}", ex);
        }
        catch (InvalidOperationException ex)
        {
            return CommandResult<string>.Failure($"Installation policy rejected: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            return CommandResult<string>.Failure($"Installation failed: {ex.Message}", ex);
        }
    }

    public override IResult<string> HandlePipedChunk(IResult<string> pipedResult, Dictionary<string, IParameterValue> parameters, IEnvironmentContext env)
    {
        var pipedPackageId = pipedResult is not null ? pipedResult.ToString() : null;
        if (String.IsNullOrWhiteSpace(pipedPackageId))
        {
            return CommandResult<string>.Failure("Piped package ID cannot be empty");
        }

        var settings = this.configService.ResolveSettings(env, parameters);

        try
        {
            var result = this.installService.InstallAsync(
                settings.NugetConfig.DefaultSource,
                pipedPackageId.Trim(),
                settings.Version,
                settings.InstallRoot,
                CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();

            if (result.IsSuccess)
            {
                return CommandResult<string>.Success(result.Message);
            }
            else
            {
                return CommandResult<string>.Failure(result.Message);
            }
        }
        catch (Exception ex)
        {
            return CommandResult<string>.Failure($"Installation failed: {ex.Message}", ex);
        }
    }

    private static IInstallService CreateInstallService()
    {
        var logger = NullLogger<NuGetClientFactory>.Instance;
        var inputValidator = new InputValidator();
        var resolver = new InstallationRootResolver(Environment.CurrentDirectory);
        var clientFactory = new NuGetClientFactory(logger);
        var commandValidator = new CommandValidationService();
        var securityPolicy = new SecurityPolicyService();

        return new InstallService(
            resolver,
            clientFactory,
            inputValidator,
            commandValidator,
            securityPolicy);
    }

    private static IPackageSourceConfigService CreateConfigService()
    {
        return new PackageSourceConfigService(new InputValidator());
    }

}
