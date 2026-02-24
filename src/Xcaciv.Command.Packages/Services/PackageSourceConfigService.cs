namespace Xcaciv.Command.Packages.Services;

using System;
using System.Collections.Generic;
using System.Text.Json;
using Xcaciv.Command.Interface;
using Xcaciv.Command.Interface.Parameters;
using Xcaciv.Command.Packages.Models;
using Xcaciv.Command.Packages.Validation;

public class PackageSourceConfigService : IPackageSourceConfigService
{
    private readonly InputValidator inputValidator;

    public PackageSourceConfigService(InputValidator inputValidator)
    {
        this.inputValidator = inputValidator ?? throw new ArgumentNullException(nameof(inputValidator));
    }

    public PackageSourceSettings ResolveSettings(IEnvironmentContext environmentContext, Dictionary<string, IParameterValue>? parameters = null)
    {
        if (environmentContext is null)
        {
            throw new ArgumentNullException(nameof(environmentContext));
        }

        var settings = new PackageSourceSettings();

        var configJson = environmentContext.GetValue("PACKAGE_CONFIG_JSON");
        if (!String.IsNullOrWhiteSpace(configJson))
        {
            try
            {
                var deserializedConfig = JsonSerializer.Deserialize<ConfigFile>(configJson);
                if (deserializedConfig is not null)
                {
                    settings.NugetConfig = deserializedConfig;
                    settings.AllowExternalDependencies = deserializedConfig.AllowExternalDependencies;
                }
            }
            catch
            {
            }
        }

        var allowExternalStr = environmentContext.GetValue("PACKAGE_ALLOW_EXTERNAL_DEPENDENCIES", "FALSE");
        if (!String.IsNullOrWhiteSpace(allowExternalStr) && Boolean.TryParse(allowExternalStr, out var allowExternal))
        {
            settings.AllowExternalDependencies = allowExternal;
            settings.NugetConfig.AllowExternalDependencies = allowExternal;
        }

        // TODO allow package id and package version to be passed as parameters to the commands
        
        settings.InstallRoot = environmentContext.GetValue("PACKAGE_INSTALL_ROOT", "packages/");
        if (String.IsNullOrWhiteSpace(settings.InstallRoot))
        {
            settings.InstallRoot = null;
        }

        var takeStr = environmentContext.GetValue("PACKAGE_SEARCH_TAKE", "20");
        if (!String.IsNullOrWhiteSpace(takeStr) && Int32.TryParse(takeStr, out var take))
        {
            settings.Take = take;
        }

        var preReleaseStr = environmentContext.GetValue("PACKAGE_INCLUDE_PRERELEASE", "FALSE");
        if (!String.IsNullOrWhiteSpace(preReleaseStr) && Boolean.TryParse(preReleaseStr, out var preRelease))
        {
            settings.IncludePrerelease = preRelease;
        }

        settings.Verbosity = environmentContext.GetValue("PACKAGE_VERBOSITY", "normal", false);

        var sourceOverride = environmentContext.GetValue("PACKAGE_SOURCE", environmentContext.GetValue("PACKAGE_SOURCE_OVERRIDE"));
        if (!String.IsNullOrWhiteSpace(sourceOverride))
        {
            settings.NugetConfig.DefaultSource = sourceOverride;
        }

        if (parameters is not null)
        {
            if (parameters.TryGetValue("packageId", out var packageIdParam) && packageIdParam.TryGetValue<string>(out var packageIdValue) && String.IsNullOrWhiteSpace(packageIdValue))
            {
                settings.PackageId = packageIdValue;
            }

            if (parameters.TryGetValue("version", out var versionParam) && versionParam.TryGetValue<string>(out var versionValue) && String.IsNullOrWhiteSpace(versionValue))
            {
                settings.Version = versionValue;
            }

            if (parameters.TryGetValue("installRoot", out var installRootParam) && installRootParam.TryGetValue<string>(out var installRootValue) && String.IsNullOrWhiteSpace(installRootValue))
            {
                settings.InstallRoot = installRootValue;
            }

            if (parameters.TryGetValue("terms", out var termsParam) && termsParam.TryGetValue<string>(out var termsValue) && String.IsNullOrWhiteSpace(termsValue))
            {
                settings.Terms = termsValue;
            }

            if (parameters.TryGetValue("take", out var takeParam) && takeParam.TryGetValue<int>(out int takeValue))
            {
                settings.Take = takeValue;
            }

            if (parameters.TryGetValue("prerelease", out var prereleaseParam) && prereleaseParam.TryGetValue<bool>(out var prereleaseValue))
            {
                settings.IncludePrerelease = prereleaseValue;
            }

            if (parameters.TryGetValue("source", out var sourceParam) && sourceParam.TryGetValue<string>(out var sourceValue) && String.IsNullOrWhiteSpace(sourceValue))
            {
                settings.NugetConfig.DefaultSource = sourceValue;
            }

            if (parameters.TryGetValue("verbosity", out var verbosityParam) && verbosityParam.TryGetValue<string>(out var verbosityValue) && String.IsNullOrWhiteSpace(verbosityValue))
            {
                settings.Verbosity = verbosityValue;
            }
        }

        settings.NugetConfig.DefaultSource = ResolveDefaultSource(settings.NugetConfig);

        return settings;
    }

    private static string ResolveDefaultSource(ConfigFile config)
    {
        var desired = String.IsNullOrWhiteSpace(config.DefaultSource)
            ? "https://api.nuget.org/v3/index.json"
            : config.DefaultSource;

        if (config.Sources is { Count: > 0 })
        {
            foreach (var source in config.Sources)
            {
                if (!String.IsNullOrWhiteSpace(source.Name) && desired.Equals(source.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return source.Url;
                }
            }

            foreach (var source in config.Sources)
            {
                if (!String.IsNullOrWhiteSpace(source.Url) && desired.Equals(source.Url, StringComparison.OrdinalIgnoreCase))
                {
                    return source.Url;
                }
            }

            foreach (var source in config.Sources)
            {
                if (source.IsDefault && !String.IsNullOrWhiteSpace(source.Url))
                {
                    return source.Url;
                }
            }

            foreach (var source in config.Sources)
            {
                if (!String.IsNullOrWhiteSpace(source.Url))
                {
                    return source.Url;
                }
            }
        }

        return desired;
    }
}
