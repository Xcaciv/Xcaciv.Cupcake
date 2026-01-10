namespace Xcaciv.Command.Packages.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;
using Xcaciv.Command.Packages.Validation;

public class SearchService : ISearchService
{
    private readonly InputValidator inputValidator;
    private readonly NuGetClientFactory clientFactory;

    public SearchService(InputValidator inputValidator, NuGetClientFactory clientFactory)
    {
        this.inputValidator = inputValidator ?? throw new ArgumentNullException(nameof(inputValidator));
        this.clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string sourceUrl, string terms, bool includePrerelease, Int32 take, CancellationToken cancellationToken)
    {
        this.inputValidator.ValidateSearchTerms(terms);

        if (take < 1)
        {
            take = 20;
        }

        if (take > 100)
        {
            take = 100;
        }

        var repository = this.clientFactory.GetRepository(sourceUrl);
        var searchResource = await repository.GetResourceAsync<PackageSearchResource>(cancellationToken).ConfigureAwait(false);
        
        var searchFilter = new SearchFilter(includePrerelease)
        {
            IncludeDelisted = false
        };
        
        var results = await searchResource.SearchAsync(
            terms,
            searchFilter,
            0,
            take,
            this.clientFactory.CreateNuGetLogger(),
            cancellationToken).ConfigureAwait(false);

        return results.Select(r => new SearchResult(r.Identity.Id, r.Identity.Version?.ToNormalizedString() ?? String.Empty, r.Summary ?? String.Empty)).ToList();
    }
}

public sealed record SearchResult(string PackageId, string Version, string Description);
