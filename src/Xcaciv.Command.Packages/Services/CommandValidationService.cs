namespace Xcaciv.Command.Packages.Services;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xcaciv.Command.Interface;

public class CommandValidationService
{
    public bool ValidateInstalledPackage(string packageDirectory)
    {
        if (String.IsNullOrWhiteSpace(packageDirectory) || !Directory.Exists(packageDirectory))
        {
            return false;
        }

        var assemblies = Directory.GetFiles(packageDirectory, "*.dll", SearchOption.AllDirectories);
        if (assemblies.Length == 0)
        {
            return false;
        }

        try
        {
            foreach (var assembly in assemblies)
            {
                if (ContainsValidCommandDelegate(assembly))
                {
                    return true;
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static bool ContainsValidCommandDelegate(string assemblyPath)
    {
        try
        {
            var assembly = Assembly.LoadFrom(assemblyPath);
            var commandInterfaceType = typeof(ICommandDelegate);

            var implementingTypes = assembly.GetTypes().Where(t =>
                !t.IsAbstract &&
                !t.IsInterface &&
                commandInterfaceType.IsAssignableFrom(t)).ToList();

            if (implementingTypes.Count == 0)
            {
                return false;
            }

            var xcacivCommandRef = assembly.GetReferencedAssemblies()
                .FirstOrDefault(a => a.Name?.StartsWith("Xcaciv.Command", StringComparison.OrdinalIgnoreCase) ?? false);

            return xcacivCommandRef is not null;
        }
        catch
        {
            return false;
        }
    }
}
