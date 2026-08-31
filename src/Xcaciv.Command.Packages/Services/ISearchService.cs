using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Xcaciv.Command.Packages.Services;

public interface ISearchService
{
    Task<IReadOnlyList<SearchResult>> SearchAsync(string sourceUrl, string terms, bool includePrerelease, Int32 take, CancellationToken cancellationToken);
}
