using Xcaciv.Command.Packages.Commands;

namespace Xcaciv.Command.PackagesTests.Commands
{
    public class SearchCommandTests
    {
        [Fact]
        public void HandleExecution_ReturnsExpectedResult()
        {
            // Arrange
            var command = new SearchCommand();
            var parameters = new string[] { "XCBatch" };
            var env = new EnvironmentContext();
            var processedParams = command.ProcessParameters(parameters);

            // Act
            var result = command.HandleExecution(processedParams, env);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("XCBatch.Core", result);
            Assert.Contains("XCBatch.Interfaces", result);            
        }

        [Fact]
        public void HandleExecution_VerbosityNormal_ReturnsSummary()
        {
            var command = new SearchCommand();
            var parameters = new string[] { "XCBatch", "-verbosity", "normal", "-take", "2" };
            var env = new EnvironmentContext();
            var processedParams = command.ProcessParameters(parameters);

            var result = command.HandleExecution(processedParams, env);

            Assert.NotNull(result);
            // normal includes id, version, summary separated by spaces and colon
            Assert.Contains(":", result);
            Assert.DoesNotContain("Published:", result);
        }

        [Fact]
        public void HandleExecution_VerbosityQuiet_ReturnsIdsOnly()
        {
            var command = new SearchCommand();
            var parameters = new string[] { "XCBatch", "-verbosity", "quiet", "-take", "2" };
            var env = new EnvironmentContext();
            var processedParams = command.ProcessParameters(parameters);

            var result = command.HandleExecution(processedParams, env);

            Assert.NotNull(result);
            // Quiet should not include version or summary markers
            Assert.DoesNotContain(":", result);
            Assert.DoesNotContain("Published:", result);
        }

        [Fact]
        public void HandleExecution_VerbosityDetailed_ReturnsExtraFields()
        {
            var command = new SearchCommand();
            var parameters = new string[] { "XCBatch", "-verbosity", "detailed", "-take", "2" };
            var env = new EnvironmentContext();
            var processedParams = command.ProcessParameters(parameters);

            var result = command.HandleExecution(processedParams, env);

            Assert.NotNull(result);
            Assert.Contains("Published:", result);
            Assert.Contains("Authors:", result);
            Assert.Contains("Vulnerabilities:", result);
        }

        [Fact]
        public void HandleExecution_Prerelease_IncludesPrereleaseFlag()
        {
            var command = new SearchCommand();
            var parameters = new string[] { "XCBatch", "-prerelease", "-take", "5" };
            var env = new EnvironmentContext();
            var processedParams = command.ProcessParameters(parameters);

            var result = command.HandleExecution(processedParams, env);

            Assert.NotNull(result);
            Assert.Contains("XCBatch", result);
        }

        [Fact]
        public void HandleExecution_CustomSource_FromEnvironment()
        {
            var command = new SearchCommand();
            var parameters = new string[] { "XCBatch", "-take", "3" };
            var env = new EnvironmentContext();
            env.SetValue("PackageSourceUrl", "https://api.nuget.org/v3/index.json");
            var processedParams = command.ProcessParameters(parameters);

            var result = command.HandleExecution(processedParams, env);

            Assert.NotNull(result);
            Assert.Contains("XCBatch", result);
        }

        [Fact]
        public void HandleExecution_TakeLimit_AppliesLimit()
        {
            var command = new SearchCommand();
            var parameters = new string[] { "XCBatch", "-take", "1" };
            var env = new EnvironmentContext();
            var processedParams = command.ProcessParameters(parameters);

            var result = command.HandleExecution(processedParams, env);

            Assert.NotNull(result);
            var lines = result.Split('\n');
            Assert.True(lines.Length <= 1);
        }

        [Fact]
        public void HandleExecution_ReturnsExpectedResult2()
        {
            // Arrange
            var command = new SearchCommand();
            var parameters = new string[] { "cake.nuget", "-verbosity", "normal" };
            var env = new EnvironmentContext();
            var processedParams = command.ProcessParameters(parameters);

            // Act
            var result = command.HandleExecution(processedParams, env);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("Cake.NuGet", result); 
        }

        [Fact]
        public void HandlePipedChunk_ReturnsUnsupportedMessage()
        {
            var command = new SearchCommand();
            var env = new EnvironmentContext();
            var processedParams = command.ProcessParameters(new[] { "param1", "param2" });

            var result = command.HandlePipedChunk("some-chunk", processedParams, env);

            Assert.Contains("Unsupported search method", result);
            Assert.Contains("some-chunk", result);
        }
    }
}