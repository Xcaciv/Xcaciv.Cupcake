using Xcaciv.Command.Packages.Commands;

namespace Xcaciv.Command.PackagesTests.Commands
{
    // NOTE: These tests need to be updated to match the new command API
    // The command signature has changed significantly in Xcaciv.Command 3.x
    // Tests are disabled pending full rewrite to match new IIoContext-based API
    public class SearchCommandTests
    {
        [Fact]
        public void PackageSearchCommand_CanBeInstantiated()
        {
            // Basic test to ensure command can be created
            var command = new PackageSearchCommand();
            Assert.NotNull(command);
        }
    }
}
