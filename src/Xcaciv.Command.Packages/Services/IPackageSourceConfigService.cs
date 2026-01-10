namespace Xcaciv.Command.Packages.Services;

using System.Collections.Generic;
using Xcaciv.Command.Interface;
using Xcaciv.Command.Interface.Parameters;
using Xcaciv.Command.Packages.Models;

public interface IPackageSourceConfigService
{
    PackageSourceSettings ResolveSettings(IEnvironmentContext environmentContext, Dictionary<string, IParameterValue>? parameters = null);
}
