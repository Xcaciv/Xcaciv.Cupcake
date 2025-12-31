using Xcaciv.Command.Interface;
using Xcaciv.Command;
using System.Security.Cryptography.X509Certificates;

namespace Xcaciv.Cupcake.Core;

public class Loop
{
    /// <summary>
    /// designates whether the install command is allowed
    /// </summary>
    public bool EnableInstallCommand { get; set; } = true;
    
    /// <summary>
    /// Enable certificate validation for secure connections
    /// </summary>
    public bool EnableCertificateValidation { get; set; } = true;
    
    /// <summary>
    /// Allowed certificate thumbprints for package sources
    /// </summary>
    public List<string> AllowedCertificateThumbprints { get; set; } = new List<string>();
    
    /// <summary>
    /// Enable signature verification for packages
    /// </summary>
    public bool EnablePackageSignatureVerification { get; set; } = true;
    
    /// <summary>
    /// string that is displayed to indicate that the user should enter a command
    /// </summary>
    public string Prompt { get; set; } = "Ɛ> ";
    /// <summary>
    /// commands that will cause the loop to exit
    /// </summary>
    public List<string> ExitCommands { get; set; } = new List<string>() { "END", "EXIT", "BYEE" };
    /// <summary>
    /// directory where packages are loaded from
    /// </summary>
    public string PackageDirectory { get; set; } = @".\packages";
    public ICommandController Controller { get; private set; } = new CommandController();
    public IEnvironmentContext Environment { get; private set; } = new EnvironmentContext();

    /// <summary>
    /// run the inputCommand loop synchronously using inputFunc to get the commandline
    /// </summary>
    /// <param name="context"></param>
    public void Run(IIoContext context, ICommandController controller, IEnvironmentContext env)
    {
        Controller = controller;
        Environment = env;

        context.SetStatusMessage("Loading Commands").Wait();

        try
        {
            controller.RegisterBuiltInCommands();
            controller.AddPackageDirectory(this.PackageDirectory);
            controller.LoadCommands();
        }
        catch (Xcaciv.Command.Interface.Exceptions.NoPluginsFoundException)
        {
            context.OutputChunk("No Plugins Found. You may want to check out `install --help`").Wait();
            // TODO: download first plugin and GOTO start again! :D
            // throw new Exceptions.LoadingException(ex.Message, ex);
        }
        catch (Exception ex)
        {
            throw new Exceptions.LoadingException("Unable to load commands.", ex);
        }

        var inputCommand = "";
        while (!this.ExitCommands.Contains(inputCommand, StringComparer.OrdinalIgnoreCase))
        {
            // run inputCommand if it was not blank
            if (!String.IsNullOrEmpty(inputCommand))
            {
                controller.Run(inputCommand, context, env).Wait();
            }
            // get next inputCommand
            inputCommand = context.PromptForCommand(this.Prompt).Result;
        }    
        
    }

    public async Task RunAsync(IIoContext context, ICommandController controller, IEnvironmentContext env)
    {
        Controller = controller;
        Environment = env;

        await context.SetStatusMessage("Loading Commands").ConfigureAwait(false);

        try
        {
            controller.AddPackageDirectory(this.PackageDirectory);
            controller.LoadCommands();
        }
        catch (Exception ex)
        {
            throw new Exceptions.LoadingException("Unable to load commands.", ex);
        }

        await context.SetStatusMessage("Done").ConfigureAwait(false);

        await Task.Run(async () => {
            var inputCommand = "";
            while (!this.ExitCommands.Contains(inputCommand, StringComparer.OrdinalIgnoreCase))
            {
                // run inputCommand if it was not blank
                if (!String.IsNullOrEmpty(inputCommand)) await controller.Run(inputCommand, context, env);
                // get next inputCommand
                inputCommand = await context.PromptForCommand(this.Prompt);

                // TODO: figure out how to handle non existing controller: download, compile
                // TODO: support NuGet style directory structure
            }
        });

    }

    public Loop RunWithDefaults()
    {

        Controller.RegisterBuiltInCommands();

        this.Run(new ConsoleContext("Cupcake Console Context", []), Controller, Environment);
        return this;
    }
}
