using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Protocol;
using NuGet.Packaging;
using NuGet.Versioning;
using NuGet.Packaging.Core;
using NuGet.Frameworks;
using System.Threading;

namespace Xcaciv.Command.Packages.Services
{
    public class NugetWrapper
    {
        public static async Task<List<IPackageSearchMetadata>> FindPackageAsync(string searchTerm, SourceRepository repository, int limit = 20, bool prerelease = false, ILogger? logger = null, CancellationToken? cancellationToken = null)
        {
            List<string> packageNames = new List<string>();
            ILogger loggerInstance = logger ?? NullLogger.Instance;
            CancellationToken cancellation = cancellationToken ?? CancellationToken.None;

            PackageSearchResource resource = await repository.GetResourceAsync<PackageSearchResource>();

            SearchFilter searchFilter = new SearchFilter(includePrerelease: prerelease);
            IEnumerable<IPackageSearchMetadata> packages = await resource.SearchAsync(searchTerm, searchFilter, 0, limit, loggerInstance, cancellation);

            return packages.ToList();            
        }

        public static async Task<List<NuGet.Versioning.NuGetVersion>> FindPackageVersionsAsync(string packageName, string packageSourceUrl)
        {
            var cache = new SourceCacheContext();

            var nugetSource = new PackageSource(packageSourceUrl);
            SourceRepository repository = Repository.Factory.GetCoreV3(nugetSource);
            var resource = await repository.GetResourceAsync<FindPackageByIdResource>();

            using (var sourceCacheContext = new SourceCacheContext())
            {
                SearchFilter searchFilter = new SearchFilter(includePrerelease: false);
                var packages = await resource.GetAllVersionsAsync(packageName, cache, NullLogger.Instance, CancellationToken.None);

                return packages.ToList();
            }
        }

        public static async Task<List<SourcePackageDependencyInfo>> ResolveDependenciesAsync(string packageId, string versionString, SourceRepository repository, NuGetFramework? framework = null, ILogger? logger = null, CancellationToken? cancellationToken = null)
        {
            List<SourcePackageDependencyInfo> packageDependencies = new List<SourcePackageDependencyInfo>();
            ILogger loggerInstance = logger ?? NullLogger.Instance;
            CancellationToken cancellation = cancellationToken ?? CancellationToken.None;
            NuGetFramework nuGetFramework = framework ?? NuGetFramework.AnyFramework;

            var package = new PackageIdentity(packageId, NuGetVersion.Parse(versionString));
            var resource = await repository.GetResourceAsync<DependencyInfoResource>();

            using (var cacheContext = new SourceCacheContext())
            {
                SourcePackageDependencyInfo dependencyInfo = await resource.ResolvePackage(package, nuGetFramework, cacheContext, loggerInstance, cancellation);

                if (dependencyInfo != null)
                {
                    packageDependencies.Add(dependencyInfo);
                }
            }

            return packageDependencies;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="identity"></param>
        /// <param name="repository"></param>
        /// <param name="targetFilePath">{path}/{packageId}.{versionString}.nupkg</param>
        /// <returns></returns>
        public static async Task<bool> DownloadPackageAsync(PackageIdentity identity, SourceRepository repository, string targetFilePath)
        {
            ILogger logger = NullLogger.Instance;
            CancellationToken cancellationToken = CancellationToken.None;
            SourceCacheContext cache = new SourceCacheContext();
            FindPackageByIdResource resource = await repository.GetResourceAsync<FindPackageByIdResource>();

            // write file stream
            using (var packageStream = new FileStream(targetFilePath, FileMode.Create, FileAccess.Write))
            {
                await resource.CopyNupkgToStreamAsync(
                    identity.Id,
                    identity.Version,
                    packageStream,
                    cache,
                    logger,
                    cancellationToken);
            }

            return true;
        }

        public static PackageIdentity GetNuspecData(string fileName)
        {
            using (var packageReader = new PackageArchiveReader(fileName))
            {
                return packageReader.NuspecReader.GetIdentity();
            }
        }

        public static void InstallPackage(PackageIdentity identity, SourceRepository repository, string targetDirectory)
        {
            string packageFileName = $"{identity.Id}.{identity.Version}.nupkg";
            string targetFilePath = Path.Combine(targetDirectory, packageFileName);

            DownloadPackageAsync(identity, repository, targetFilePath).Wait();

            PackageIdentity packageIdentity = GetNuspecData(targetFilePath);
            string packageDirectory = Path.Combine(targetDirectory, packageIdentity.Id, packageIdentity.Version.ToString());

            if (!Directory.Exists(packageDirectory))
            {
                Directory.CreateDirectory(packageDirectory);
            }
            // TODO: Extract package to directory
            // TODO: resolve dependencies
            // ZipFile.ExtractToDirectory(targetFilePath, packageDirectory);
        }

       
    }
}
