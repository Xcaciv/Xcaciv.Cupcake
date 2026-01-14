namespace Xcaciv.Command.Packages.Commands;

using System;
using System.Collections.Generic;
using System.Threading;
using Xcaciv.Command.Core;
using Xcaciv.Command.Interface;
using Xcaciv.Command.Interface.Attributes;
using Xcaciv.Command.Interface.Parameters;
using Xcaciv.Command.Packages.Abstractions;
using Xcaciv.Command.Packages.Services;
using Xcaciv.Command.Packages.Validation;

[CommandRegister("search", "Search for command packages using natural language terms")]
[CommandRoot("package", "Manage command packages")]
[CommandParameterOrdered("terms", "Search terms")]
[CommandParameterNamed("take", "Number of results to return (default 20, max 100)", DataType = typeof(int))]
[CommandFlag("prerelease", "Include prerelease packages", DataType = typeof(bool))]
[CommandParameterNamed("source", "Package source URL (HTTPS)")]
[CommandParameterNamed("verbosity", "quiet|normal|detailed", AllowedValues = ["quiet", "normal", "detailed"])]
public class PackageSearchCommand : AbstractPackageCommand
{
    private ISearchService searchService;
    private IPackageSourceConfigService configService;

    public PackageSearchCommand()
        : base()
    {
        searchService = CreateSearchService();
        configService = CreateConfigService();
    }

    public PackageSearchCommand(NuGetIoContextLoggerFactory loggerFactory)
        : base(loggerFactory)
    {
        searchService = CreateSearchService();
        configService = CreateConfigService();
    }

    public PackageSearchCommand(ISearchService searchService, IPackageSourceConfigService configService, NuGetIoContextLoggerFactory loggerFactory)
        : base(loggerFactory)
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

    private SearchService CreateSearchService()
    {
        var logger = new NuGetIoContextLogger<NuGetClientFactory>();
        var clientFactory = new NuGetClientFactory(logger);
        var inputValidator = new InputValidator();
        return new SearchService(inputValidator, clientFactory);
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
