using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xcaciv.Command.Interface;

namespace Xcaciv.Cupcake
{
    /// <summary>
    /// console messge context
    /// </summary>
    internal class OutputMessaeContext : ITextIoContext
    {
        public Guid Id {get; set;} = Guid.NewGuid();

        public Guid? Parent {get; set;}

        public string Name => throw new NotImplementedException();

        public Task<ITextIoContext> GetChild()
        {
            return Task.FromResult(new OutputMessaeContext() { Parent = this.Id } as ITextIoContext);
        }

        public Task<ITextIoContext> GetChild(string Name)
        {
            throw new NotImplementedException();
        }

        public Task<string> PromptForCommand(string prompt)
        {
            throw new NotImplementedException();
        }

        public Task<int> SetProgress(int total, int step)
        {
            throw new NotImplementedException();
        }

        public Task SetStatusMessage(string message)
        {
            throw new NotImplementedException();
        }

        public Task WriteLine(string message)
        {
            throw new NotImplementedException();
        }
    }
}
