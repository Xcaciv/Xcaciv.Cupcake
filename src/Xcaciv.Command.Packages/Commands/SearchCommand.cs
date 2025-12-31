using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using Xcaciv.Command.Core;
using Xcaciv.Command.Interface;
using Xcaciv.Command.Interface.Attributes;
using Xcaciv.Command.Interface.Parameters;
using Xcaciv.Command.Packages.Services;

namespace Xcaciv.Command.Packages.Commands
{
    [CommandRoot("Package", "Package management commands")]
    [CommandRegister("Search", "find a package")]
    [CommandParameterOrdered("search_terms", "String associated to the desired package.", IsRequired = true)]
    [CommandFlag("prerelease", "Include prerelease packages")]
    [CommandParameterNamed("source", "Package source URL (HTTPS)")]
    [CommandParameterNamed("verbosity", "The level of detail to display in the output.", AllowedValues = ["quiet", "normal", "detailed"], DefaultValue = "normal")]
    [CommandParameterNamed("take", "Limit the number of results to return.", DefaultValue = "20")]
    public class SearchCommand : AbstractCommand
    {
        public override string HandleExecution(Dictionary<string, IParameterValue> parameters, IEnvironmentContext env)
        {
            var packageSourceUrl = env.GetValue("PackageSourceUrl");
            // default to nuget.org
            if (string.IsNullOrEmpty(packageSourceUrl))
            {
                packageSourceUrl = "https://api.nuget.org/v3/index.json";
            }

            // Enforce HTTPS only for security
            if (!Uri.TryCreate(packageSourceUrl, UriKind.Absolute, out var packageSourceUri) || packageSourceUri.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidOperationException("Insecure or invalid package source URL. HTTPS is required.");
            }

            // Create a SourceRepository from the packageSourceUrl
            var packageSource = new PackageSource(packageSourceUrl);
            var repository = Repository.Factory.GetCoreV3(packageSource);

            List<string> searchResult = [];

            // Clamp limit to prevent abuse
            var takeParam = parameters["take"];
            var takeStr = takeParam.GetType().GetProperty("RawValue")?.GetValue(takeParam)?.ToString() ?? "20";
            var requestedTake = int.Parse(takeStr);
            int limit = Math.Clamp(requestedTake, 1, 100);
            bool prerelease = parameters.ContainsKey("prerelease");

            // Validate search terms
            var searchTermsParam = parameters["search_terms"];
            var searchTerms = (searchTermsParam.GetType().GetProperty("RawValue")?.GetValue(searchTermsParam)?.ToString() ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(searchTerms))
            {
                return string.Empty;
            }
            if (searchTerms.Length > 200)
            {
                searchTerms = searchTerms.Substring(0, 200);
            }

            var tmpResult = NugetWrapper.FindPackageAsync(searchTerms, repository, limit, prerelease).Result;

            var verbosity = "normal";
            if (parameters.ContainsKey("verbosity"))
            {
                var verbosityParam = parameters["verbosity"];
                verbosity = verbosityParam.GetType().GetProperty("RawValue")?.GetValue(verbosityParam)?.ToString() ?? "normal";
            }
            
            switch (verbosity)
            {
                case "quiet":
                    searchResult = tmpResult.Select(p => p.Identity.Id).ToList();
                    break;
                case "normal":
                    searchResult = tmpResult.Select(p => $"{p.Identity.Id} {p.Identity.Version} : {p.Summary}").ToList();
                    break;
                case "detailed":
                    searchResult = tmpResult.Select(p => $"{p.Identity.Id} {p.Identity.Version} ({p.DownloadCount}) : {p.Summary}" +
                    $"\n  Published:{p.Published}" +
                    $"\n  Authors:{p.Authors}" +
                    $"\n  License:{p.LicenseMetadata}" +
                    $"\n  Vulnerabilities:{p.Vulnerabilities.Count()}" +
                    $"\n---").ToList();
                    break;
                default:
                    // Fallback to normal if invalid verbosity provided
                    searchResult = tmpResult.Select(p => $"{p.Identity.Id} {p.Identity.Version} : {p.Summary}").ToList();
                    break;
            }

            return string.Join("\n", searchResult);
        }

        public override string HandlePipedChunk(string pipedChunk, Dictionary<string, IParameterValue> parameters, IEnvironmentContext env)
        {
            return $"Unsupported search method for {pipedChunk} (piped)" + string.Join(',', parameters.Keys);
        }

    }
}
