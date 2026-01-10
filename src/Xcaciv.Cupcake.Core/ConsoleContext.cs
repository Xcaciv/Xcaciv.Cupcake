using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xcaciv.Command;
using Xcaciv.Command.Core;
using Xcaciv.Command.Interface;

namespace Xcaciv.Cupcake.Core
{
    public class ConsoleContext(string name = "ConsoleIo", string[]? parameters = default, Guid? parentId = default, bool verbose = true) : AbstractTextIo(name, [.. parameters], parentId)
    {
        /// <summary>
        /// template for progress text output
        /// 0 - context name
        /// 1 - int progress
        /// </summary>
        public string ProgressTemplate { get; set; } = "{0} progress {1}%";

        // format color defaults
        public ConsoleColor ForegroundColor { get; set; } = ConsoleColor.Blue;
        public ConsoleColor BackgroundColor { get; set; } = ConsoleColor.Black;

        public ConsoleColor ErrorColor { get; set; } = ConsoleColor.Red;

        public ConsoleColor StatusForegroundColor { get; set; } = ConsoleColor.Yellow;
        public ConsoleColor StatusBackgroundColor { get; set; } = ConsoleColor.DarkBlue;

        public ConsoleColor PromptForegroundColor { get; set; } = ConsoleColor.Green;
        public ConsoleColor PromptBackgroundColor { get; set; } = ConsoleColor.Black;
        public new bool Verbose { get; set; } = verbose;

        /// <summary>
        /// <see cref="IIoContext"/>
        /// </summary>
        /// <param name="childParameters"></param>
        /// <returns></returns>
        public override Task<IIoContext> GetChild(string[]? childParameters = null)
        {
            var child = new ConsoleContext(this.Name + "Child", childParameters, Id);

            if (this.HasPipedInput && this.inputPipe != null) child.SetInputPipe(this.inputPipe);

            if (this.outputPipe != null) child.SetOutputPipe(this.outputPipe);

            return Task.FromResult<IIoContext>(child);
        }
        /// <summary>
        /// <see cref="AbstractTextIo"/>
        /// </summary>
        /// <param name="chunk"></param>
        /// <returns></returns>
        public override Task HandleOutputChunk(IResult<string> chunk)
        {
            var output = chunk.ToString();
            if (!String.IsNullOrEmpty(output))
            {
                if (chunk.IsSuccess)
                {
                    Console.ForegroundColor = ForegroundColor;
                    Console.BackgroundColor = BackgroundColor;
                }
                else
                {
                    Console.ForegroundColor = ErrorColor;
                    Console.BackgroundColor = BackgroundColor;
                }
                
                Console.WriteLine(output);
                Console.ResetColor();
            }
            return Task.CompletedTask;
        }
        /// <summary>
        /// get input from user
        /// </summary>
        /// <param name="prompt"></param>
        /// <returns></returns>
        public override Task<string> PromptForCommand(string prompt)
        {
            Console.ForegroundColor = PromptForegroundColor;
            Console.BackgroundColor = PromptBackgroundColor;
            Console.Write(prompt);
            return Task.FromResult(Console.ReadLine() ?? string.Empty);
        }
        /// <summary>
        /// do some progress math, print a progress message and return the int progress
        /// </summary>
        /// <param name="total"></param>
        /// <param name="step"></param>
        /// <returns></returns>
        public override Task<int> SetProgress(int total, int step)
        {
            int progress = total / step;
            SetStatusMessage(string.Format(ProgressTemplate, this.Name, progress));
            return Task.FromResult(progress);
        }
        /// <summary>
        /// output status message to console
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public override Task SetStatusMessage(string message)
        {
            if (Verbose == false)
            {
                Debug.WriteLine(message);
                return Task.CompletedTask;
            }

            Console.ForegroundColor = StatusForegroundColor;
            Console.BackgroundColor = StatusBackgroundColor;
            Console.WriteLine(message);
            Console.ResetColor();
            return Task.CompletedTask;
        }
    }
}
