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

[CommandRegister("search", "Search for command packages using natural language terms")]
[CommandRoot("package", "Manage command packages")]
[CommandParameterOrdered("terms", "Search terms")]
[CommandParameterNamed("take", "Number of results to return (default 20, max 100)")]
[CommandFlag("prerelease", "Include prerelease packages")]
[CommandParameterNamed("source", "Package source URL (HTTPS)")]
[CommandParameterNamed("verbosity", "quiet|normal|detailed")]
public class PackageSearchCommand : AbstractCommand
{
    private readonly ISearchService searchService;
    private readonly IPackageSourceConfigService configService;

    public PackageSearchCommand()
        : this(CreateSearchService(), CreateConfigService())
    {
    }

    public PackageSearchCommand(ISearchService searchService, IPackageSourceConfigService configService)
    {
        this.searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
        this.configService = configService ?? throw new ArgumentNullException(nameof(configService));
    }

    public override IResult<string> HandleExecution(Dictionary<string, IParameterValue> parameters, IEnvironmentContext env)
    {
        var settings = this.configService.ResolveSettings(env, parameters);

        var results = this.searchService.SearchAsync(settings.NugetConfig.DefaultSource, settings.Terms, settings.IncludePrerelease, settings.Take, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
        return CommandResult<string>.Success(Render(results));
    }

    public override IResult<string> HandlePipedChunk(IResult<string> pipedResult, Dictionary<string, IParameterValue> parameters, IEnvironmentContext env)
    {
        var pipedText = pipedResult is not null ? pipedResult.ToString() : String.Empty;
        var settings = this.configService.ResolveSettings(env, parameters);
        var combinedTerms = String.IsNullOrWhiteSpace(settings.Terms) ? pipedText : $"{settings.Terms} {pipedText}";

        var results = this.searchService.SearchAsync(settings.NugetConfig.DefaultSource, combinedTerms ?? String.Empty, settings.IncludePrerelease, settings.Take, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
        return CommandResult<string>.Success(Render(results));
    }

    private static ISearchService CreateSearchService()
    {
        var logger = NullLogger<NuGetClientFactory>.Instance;
        var clientFactory = new NuGetClientFactory(logger);
        var inputValidator = new InputValidator();
        return new SearchService(inputValidator, clientFactory);
    }

    private static IPackageSourceConfigService CreateConfigService()
    {
        return new PackageSourceConfigService(new InputValidator());
    }

    private static string Render(IReadOnlyList<SearchResult> results)
    {
        if (results.Count == 0)
        {
            return "No packages found.";
        }

        var lines = new List<string>
        {
            "PackageId | Version | Description",
            new string('-', 80)
        };

        foreach (var result in results)
        {
            lines.Add($"{result.PackageId} | {result.Version} | {result.Description}");
        }

        return String.Join(Environment.NewLine, lines);
    }
}
