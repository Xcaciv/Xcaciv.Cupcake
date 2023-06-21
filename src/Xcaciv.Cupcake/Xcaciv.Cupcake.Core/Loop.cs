using Xcaciv.Command.Interface;
using Xcaciv.Command;

namespace Xcaciv.Cupcake.Core;

public class Loop
{
    public string Prompt { get; set; } = "Ɛ> ";
    public List<string> ExitCommands { get; set; } = new List<string>() { "END", "EXIT", "BYEE" };
    public string PackageDirectory { get; set; } = @".\packages";
    public async Task RunAsync(ITextIoContext context)
    {
        await context.SetStatusMessage("Loading Commands").ConfigureAwait(false);
        var commands = new CommandController();

        try
        {
            commands.AddPackageDirectory(this.PackageDirectory);
            commands.LoadCommands();
        }
        catch (Exception ex)
        {
            throw new Exceptions.LoadingException("Unable to load commands.", ex);
        }

        await context.SetStatusMessage("Done").ConfigureAwait(false);

        await Task.Run(async () => {
            var command = "";
            while (!this.ExitCommands.Contains(command, StringComparer.OrdinalIgnoreCase))
            {
                // run command if it was not blank
                if (String.IsNullOrEmpty(command)) await commands.Run(command, context);
                // get next command
                command = await context.PromptForCommand(this.Prompt).ConfigureAwait(false);

                // TODO: figure out how to handle non existing commands: download, compile
                // TODO: support NuGet style directory structure
            }
        });
        
    }
    /// <summary>
    /// run the command loop synchronously using inputFunc to get the commandline
    /// </summary>
    /// <param name="context"></param>
    public void Run(ITextIoContext context)
    {
        context.SetStatusMessage("Loading Commands").Wait();
        var commands = new CommandController();

        try
        {
            commands.AddPackageDirectory(this.PackageDirectory);
            commands.LoadCommands();
        }
        catch (Xcaciv.Command.Exceptions.NoPluginsFoundException ex)
        {
            // TODO: download first plugin and GOTO start again! :D
            throw new Exceptions.LoadingException(ex.Message, ex);
        }
        catch (Exception ex)
        {
            throw new Exceptions.LoadingException("Unable to load commands.", ex);
        }

        context.SetStatusMessage("Done").Wait();

        var command = "";
        while (!this.ExitCommands.Contains(command, StringComparer.OrdinalIgnoreCase))
        {
            // run command if it was not blank
            if (String.IsNullOrEmpty(command)) commands.Run(command, context).Wait();
            // get next command
            command = context.PromptForCommand(this.Prompt).Result;
        }    
        
    }
}
