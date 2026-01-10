// See https://aka.ms/new-console-template for more information
using Xcaciv.Command.Interface;
using Xcaciv.Command.Packages.Commands;

try
{
    var commandLoop = new Xcaciv.Cupcake.Core.Loop();
    var packageSearchCommand = (ICommandDelegate)new PackageSearchCommand();
    commandLoop.Controller.AddCommand("search", packageSearchCommand, false);
    commandLoop.RunWithDefaults();
}
catch (Exception ex)
{
    Console.WriteLine($"Error {ex.Message}");
    // exit in error state
    Environment.Exit(1);
}

// TODO:
//  - //  - 