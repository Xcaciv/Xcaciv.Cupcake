using System;
using System.Collections.Generic;
using System.Threading;
using Xcaciv.Command.Core;
using Xcaciv.Command.Interface;
using Xcaciv.Command.Interface.Attributes;
using Xcaciv.Command.Interface.Parameters;
using Xcaciv.Command.Packages.Abstractions;
using Xcaciv.Command.Packages.Models;
using Xcaciv.Command.Packages.Services;
using Xcaciv.Command.Packages.Validation;

namespace Xcaciv.Command.Packages.Commands;

[CommandRegister("install", "Install a command package from a NuGet source")]
[CommandRoot("package", "Manage command packages")]
[CommandParameterOrdered("packageId", "Package identifier")]
[CommandParameterNamed("version", "Specific version to install (optional; latest used if omitted)")]
[CommandParameterNamed("source", "Package source URL (HTTPS)")]
[CommandParameterNamed("installRoot", "Override installation root directory")]
public class PackageInstallCommand : AbstractPackageCommand
{
    private IInstallService installService;
    private IPackageSourceConfigService configService;

    public PackageInstallCommand()
        : base()
    {
        installService = CreateInstallService();
        configService = CreateConfigService();
    }

    public PackageInstallCommand(NuGetIoContextLoggerFactory loggerFactory)
        : base(loggerFactory)
    {
        installService = CreateInstallService();
        configService = CreateConfigService();
    }

    public PackageInstallCommand(IInstallService installService, IPackageSourceConfigService configService, NuGetIoContextLoggerFactory loggerFactory)
        : base(loggerFactory)
    {
        this.installService = installService ?? throw new ArgumentNullException(nameof(installService));
        this.configService = configService ?? throw new ArgumentNullException(nameof(configService));
    }

    public override IResult<string> HandleExecution(Dictionary<string, IParameterValue> parameters, IEnvironmentContext env)
    {
        try
        {
            // Ensure parameters dict is not null
            if (parameters is null)
            {
                parameters = new Dictionary<string, IParameterValue>();
            }

            var settings = this.configService.ResolveSettings(env, parameters);
            if (String.IsNullOrWhiteSpace(settings.PackageId))
            {
                return CommandResult<string>.Failure("A package ID is required to install a package.");
            }

            var result = ExecuteInstall(settings);
            return result.IsSuccess
                ? CommandResult<string>.Success(result.Message)
                : CommandResult<string>.Failure(result.Message);
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
        var pipedPackageId = pipedResult is not null ? pipedResult.Output : null;
        if (String.IsNullOrWhiteSpace(pipedPackageId))
        {
            return CommandResult<string>.Failure("Piped package ID cannot be empty");
        }

        try
        {
            // Ensure parameters dict is not null
            if (parameters is null)
            {
                parameters = new Dictionary<string, IParameterValue>();
            }

            var settings = this.configService.ResolveSettings(env, parameters);
            var modifiedSettings = new PackageSourceSettings
            {
                NugetConfig = settings.NugetConfig,
                PackageId = pipedPackageId.Trim(),
                Version = settings.Version,
                InstallRoot = settings.InstallRoot,
                Terms = settings.Terms,
                Take = settings.Take,
                IncludePrerelease = settings.IncludePrerelease,
                Verbosity = settings.Verbosity,
                AllowExternalDependencies = settings.AllowExternalDependencies
            };
            var result = ExecuteInstall(modifiedSettings);
            return result.IsSuccess
                ? CommandResult<string>.Success(result.Message)
                : CommandResult<string>.Failure(result.Message);
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

    private InstallResult ExecuteInstall(PackageSourceSettings settings)
    {
        return this.installService.InstallAsync(
            settings.NugetConfig.DefaultSource,
            settings.PackageId,
            settings.Version,
            settings.InstallRoot,
            CancellationToken.None)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();
    }

    private InstallService CreateInstallService()
    {
        var logger = new NuGetIoContextLogger<NuGetClientFactory>();
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
}
