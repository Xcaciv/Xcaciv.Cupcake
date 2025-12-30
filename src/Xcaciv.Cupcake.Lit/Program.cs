// See https://aka.ms/new-console-template for more information
//using Xcaciv.Command.Packages;

try
{
    var commandLoop = new Xcaciv.Cupcake.Core.Loop();
    //commandLoop.Controller.AddCommand("internal", new InstallCommand());
    //commandLoop.Controller.AddCommand("internal", new SearchCommand());
    commandLoop.RunWithDefaults();
}
catch (Exception ex)
{
    Console.WriteLine($"Error {ex.Message}");
    // exit in error state
    Environment.Exit(1);
}

// TODO:
//  - 