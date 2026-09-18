using System.Collections.Generic;
using Xcaciv.Command.Interface;
using Xcaciv.Command.Interface.Parameters;
using Xcaciv.Command.Packages.Models;

namespace Xcaciv.Command.Packages.Services;

public interface IPackageSourceConfigService
{
    PackageSourceSettings ResolveSettings(IEnvironmentContext environmentContext, Dictionary<string, IParameterValue>? parameters = null);
}
