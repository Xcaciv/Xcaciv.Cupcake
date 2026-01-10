using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Xcaciv.Command.Packages.Services;

namespace Xcaciv.Command.PackagesTests.Services
{
    // NOTE: NugetWrapper has been replaced with NuGetClientFactory and SearchService.
    // These tests are disabled until they can be updated to test the new service architecture.
    public class NugetWrapperTests
    {
        // Tests commented out - NugetWrapper class no longer exists
        // Update these tests to use NuGetClientFactory and SearchService instead
        
        /*
        [Fact]
        public async Task FindPackageAsync_ReturnsExpectedPackages()
        {
            // Arrange
            string searchTerm = "XCBatch";
            string packageSourceUrl = "https://api.nuget.org/v3/index.json";
            var nugetSource = new PackageSource(packageSourceUrl);
            SourceRepository repository = Repository.Factory.GetCoreV3(nugetSource);

            int limit = 20;
            bool prerelease = false;

            // Act
            var packages = await NugetWrapper.FindPackageAsync(searchTerm, repository, limit, prerelease);

            // Assert
            Assert.NotNull(packages);
            Assert.True(packages.Count > 0);
        }

        [Fact]
        public async Task FindPackageAsync_PrereleaseTrue_ReturnsPackages()
        {
            // Arrange
            string searchTerm = "XCBatch";
            string packageSourceUrl = "https://api.nuget.org/v3/index.json";
            var nugetSource = new PackageSource(packageSourceUrl);
            SourceRepository repository = Repository.Factory.GetCoreV3(nugetSource);

            int limit = 10;
            bool prerelease = true;

            // Act
            var packages = await NugetWrapper.FindPackageAsync(searchTerm, repository, limit, prerelease);

            // Assert
            Assert.NotNull(packages);
            Assert.True(packages.Count > 0);
        }

        [Fact]
        public async Task FindPackageVersionsAsync_ReturnsExpectedVersions()
        {
            // Arrange
            string packageName = "XCBatch.Core";
            string packageSourceUrl = "https://api.nuget.org/v3/index.json";

            // Act
            var versions = await NugetWrapper.FindPackageVersionsAsync(packageName, packageSourceUrl);

            // Assert
            Assert.NotNull(versions);
            Assert.True(versions.Count > 0);
        }

        [Fact]
        public async Task FindPackageVersionsAsync_InvalidPackage_ReturnsEmpty()
        {
            // Arrange
            string packageName = "Package.Does.Not.Exist";
            string packageSourceUrl = "https://api.nuget.org/v3/index.json";

            // Act
            var versions = await NugetWrapper.FindPackageVersionsAsync(packageName, packageSourceUrl);

            // Assert
            Assert.NotNull(versions);
            Assert.True(versions.Count == 0);
        }

        [Fact]
        public async Task DownloadPackageAsync_ReturnsTrue()
        {
            // Arrange
            string packageId = "XCBatch.Core";
            string versionString = "1.0.0";
            string packageSourceUrl = "https://api.nuget.org/v3/index.json";
            string filePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".nupkg");

            var nugetSource = new PackageSource(packageSourceUrl);
            SourceRepository repository = Repository.Factory.GetCoreV3(nugetSource);

            var packageIdentity = new PackageIdentity(packageId, NuGetVersion.Parse(versionString));

            // Act
            var result = await NugetWrapper.DownloadPackageAsync(packageIdentity, repository, filePath);

            // Assert
            Assert.True(result);
            if (File.Exists(filePath)) File.Delete(filePath);
        }
        */
    }
}
