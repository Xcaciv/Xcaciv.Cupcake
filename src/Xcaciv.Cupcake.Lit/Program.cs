// See https://aka.ms/new-console-template for more information
using Xcaciv.Command.Interface;
using Xcaciv.Command.Packages.Commands;
using Xcaciv.Command;
using Xcaciv.Command.FileLoader;

try
{
    var loadEnv = args.Contains("-loadenv", StringComparer.OrdinalIgnoreCase);
    var customConfigPath = args.FirstOrDefault(arg => !arg.StartsWith("-"));

    var commandLoop = new Xcaciv.Cupcake.Core.Loop();
    

    var appRoot = AppContext.BaseDirectory;
    var defaultConfigPath = Path.Combine(appRoot, "lit.cupcake.config.yml");
    var envFilePath = Path.Combine(appRoot, "lit.cupcake.env.yml");

    var environment = loadEnvironment(defaultConfigPath, envFilePath, loadEnv, customConfigPath??"");

    commandLoop.RunInConsoleMode(environment);

    // overwrite environment on exit
    if (File.Exists(envFilePath)) File.Delete(envFilePath);
    environment = commandLoop.Controller.GetEnvironment();
    (new EnvironmentFileManager()).SaveEnvironment(envFilePath, environment);
    Console.WriteLine($"Environment saved to {envFilePath}");
}
catch (Exception ex)
{
    Console.WriteLine($"Error {ex}");
    Environment.Exit(1);
}

static IControllerEnvironmentContext loadEnvironment(string defaultConfigPath, string envFilePath, bool loadEnv = false, string customConfigPath = "")
{
    var environment = new ControllerEnvironmentContext();
    var configLoader = new ControllerEnvironmentFileManager();

    if (loadEnv && File.Exists(envFilePath))
    {
        var envConfig = configLoader.LoadControllerEnvironmentFromFile(envFilePath);
        if (envConfig is not null)
        {
            environment.UpdateEnvironment(envConfig.GetEnvironment());
        }
    }

    if (File.Exists(defaultConfigPath))
    {
        var loadedEnv = configLoader.LoadControllerEnvironmentFromFile(defaultConfigPath);
        if (loadedEnv is not null)
        {
            environment.UpdateEnvironment(loadedEnv.GetEnvironment());
        }
    }

    if (!String.IsNullOrWhiteSpace(customConfigPath) && File.Exists(customConfigPath))
    {
        var customConfig = configLoader.LoadControllerEnvironmentFromFile(customConfigPath);
        if (customConfig is not null)
        {
            environment.UpdateEnvironment(customConfig.GetEnvironment());
        }
    }

    return environment;
}

// TODO:
//  - //  - 