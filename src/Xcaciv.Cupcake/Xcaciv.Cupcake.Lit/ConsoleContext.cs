using Xcaciv.Command.Interface;

namespace Xcaciv.Cupcake.Lit;

/// <summary>
/// console messge context that operates syncornesly
/// </summary>
internal class ConsoleContext : ITextIoContext
{
    /// <summary>
    /// current id
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>
    /// id of parent
    /// </summary>
    public Guid? Parent { get; set; }
    /// <summary>
    /// friendly name
    /// </summary>
    public string Name { get; set; } = "Default";
    /// <summary>
    /// template for progress text output
    /// 0 - context name
    /// 1 - int progress
    /// </summary>
    public string ProgressTemplate { get; set; } = "{0} progress {1}%";
    /// <summary>
    /// get a new
    /// </summary>
    /// <returns></returns>
    public Task<ITextIoContext> GetChild(string name)
    {
        return Task.FromResult(new ConsoleContext() { Parent = Id, Name = name, ProgressTemplate = this.ProgressTemplate } as ITextIoContext);
    }
    /// <summary>
    /// get input from user
    /// </summary>
    /// <param name="prompt"></param>
    /// <returns></returns>
    public Task<string> PromptForCommand(string prompt)
    {
        Console.Write(prompt);
        return Task.FromResult(Console.ReadLine()??String.Empty);
    }
    /// <summary>
    /// do some progress math, print a progress message and return the int progress
    /// </summary>
    /// <param name="total"></param>
    /// <param name="step"></param>
    /// <returns></returns>
    public Task<int> SetProgress(int total, int step)
    {
        int progress = total / step;
        this.WriteLine(String.Format(this.ProgressTemplate, this.Name, progress));
        return Task.FromResult(progress);
    }
    /// <summary>
    /// output status message to console
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public Task SetStatusMessage(string message)
    {
        Console.WriteLine(message);
        return Task.CompletedTask;
    }
    /// <summary>
    /// write message to console
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public Task WriteLine(string message)
    {
        Console.WriteLine(message);
        return Task.CompletedTask;
    }
}
