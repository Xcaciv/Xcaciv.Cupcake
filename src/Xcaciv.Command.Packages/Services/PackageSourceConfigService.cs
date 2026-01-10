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

        var configJson = environmentContext.GetValue("XCACIV_PACKAGE_CONFIG_JSON");
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

        var sourceUrl = environmentContext.GetValue("XCACIV_PACKAGE_SOURCE");
        if (!String.IsNullOrWhiteSpace(sourceUrl))
        {
            settings.NugetConfig.DefaultSource = sourceUrl;
        }

        var allowExternalStr = environmentContext.GetValue("XCACIV_PACKAGE_ALLOW_EXTERNAL_DEPENDENCIES");
        if (!String.IsNullOrWhiteSpace(allowExternalStr) && Boolean.TryParse(allowExternalStr, out var allowExternal))
        {
            settings.AllowExternalDependencies = allowExternal;
            settings.NugetConfig.AllowExternalDependencies = allowExternal;
        }

        settings.PackageId = environmentContext.GetValue("XCACIV_PACKAGE_ID", String.Empty, false);

        var versionStr = environmentContext.GetValue("XCACIV_PACKAGE_VERSION");
        if (!String.IsNullOrWhiteSpace(versionStr))
        {
            settings.Version = versionStr;
        }

        settings.InstallRoot = environmentContext.GetValue("XCACIV_INSTALL_ROOT");
        if (String.IsNullOrWhiteSpace(settings.InstallRoot))
        {
            settings.InstallRoot = null;
        }

        settings.Terms = environmentContext.GetValue("XCACIV_SEARCH_TERMS", String.Empty, false);

        var takeStr = environmentContext.GetValue("XCACIV_SEARCH_TAKE");
        if (!String.IsNullOrWhiteSpace(takeStr) && Int32.TryParse(takeStr, out var take))
        {
            settings.Take = take;
        }

        var preReleaseStr = environmentContext.GetValue("XCACIV_INCLUDE_PRERELEASE");
        if (!String.IsNullOrWhiteSpace(preReleaseStr) && Boolean.TryParse(preReleaseStr, out var preRelease))
        {
            settings.IncludePrerelease = preRelease;
        }

        settings.Verbosity = environmentContext.GetValue("XCACIV_VERBOSITY", "normal", false);

        var sourceOverride = environmentContext.GetValue("XCACIV_SOURCE_OVERRIDE");
        if (!String.IsNullOrWhiteSpace(sourceOverride))
        {
            settings.NugetConfig.DefaultSource = sourceOverride;
        }

        if (parameters is not null)
        {
            if (parameters.TryGetValue("packageId", out var packageIdParam))
            {
                settings.PackageId = packageIdParam.ToString() ?? String.Empty;
            }

            if (parameters.TryGetValue("version", out var versionParam))
            {
                var versionValue = versionParam.ToString();
                settings.Version = String.IsNullOrWhiteSpace(versionValue) ? null : versionValue;
            }

            if (parameters.TryGetValue("installRoot", out var installRootParam))
            {
                var installRootValue = installRootParam.ToString();
                settings.InstallRoot = String.IsNullOrWhiteSpace(installRootValue) ? null : installRootValue;
            }

            if (parameters.TryGetValue("terms", out var termsParam))
            {
                settings.Terms = termsParam.ToString() ?? String.Empty;
            }

            if (parameters.TryGetValue("take", out var takeParam) && Int32.TryParse(takeParam.ToString(), out var takeValue))
            {
                settings.Take = takeValue;
            }

            if (parameters.TryGetValue("prerelease", out var prereleaseParam))
            {
                if (Boolean.TryParse(prereleaseParam.ToString(), out var prereleaseValue))
                {
                    settings.IncludePrerelease = prereleaseValue;
                }
            }

            if (parameters.TryGetValue("source", out var sourceParam))
            {
                var sourceValue = sourceParam.ToString();
                if (!String.IsNullOrWhiteSpace(sourceValue))
                {
                    settings.NugetConfig.DefaultSource = sourceValue;
                }
            }

            if (parameters.TryGetValue("verbosity", out var verbosityParam))
            {
                settings.Verbosity = verbosityParam.ToString() ?? "normal";
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
