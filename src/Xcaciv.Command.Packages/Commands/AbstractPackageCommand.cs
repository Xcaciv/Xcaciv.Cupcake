using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xcaciv.Command.Core;
using Xcaciv.Command.Interface;
using Xcaciv.Command.Interface.Attributes;
using Xcaciv.Command.Interface.Parameters;
using Xcaciv.Command.Packages.Abstractions;
using Xcaciv.Command.Packages.Services;
using Xcaciv.Command.Packages.Validation;

namespace Xcaciv.Command.Packages.Commands
{
    public abstract class AbstractPackageCommand : Xcaciv.Command.Core.AbstractCommand
    {
        protected readonly NuGetIoContextLoggerFactory LoggerFactory;

        protected AbstractPackageCommand()
            : this(new NuGetIoContextLoggerFactory())
        {
        }

        protected AbstractPackageCommand(NuGetIoContextLoggerFactory loggerFactory)
        {
            this.LoggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        public new async IAsyncEnumerable<IResult<string>> Main(IIoContext io, IEnvironmentContext environment)
        {
            // Set the IIoContext for the logger at the start of execution
            LoggerFactory.SetIoContext(io);

            try
            {
                await foreach (var result in base.Main(io, environment))
                {
                    yield return result;
                }
            }
            finally
            {
                // Clear the IIoContext after execution completes
                LoggerFactory.ClearIoContext();
            }
        }

        protected PackageSourceConfigService CreateConfigService()
        {
            return new PackageSourceConfigService(new InputValidator());
        }
    }
}
