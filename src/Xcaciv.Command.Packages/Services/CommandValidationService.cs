using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Xcaciv.Command.Packages.Services;

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

        foreach (var assembly in assemblies)
        {
            if (ContainsValidCommandDelegate(assembly))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsValidCommandDelegate(string assemblyPath)
    {
        try
        {
            using var stream = File.OpenRead(assemblyPath);
            using var peReader = new PEReader(stream);

            if (!peReader.HasMetadata)
            {
                return false;
            }

            var metadataReader = peReader.GetMetadataReader();

            var hasXcacivRef = false;
            foreach (var arHandle in metadataReader.AssemblyReferences)
            {
                var ar = metadataReader.GetAssemblyReference(arHandle);
                if (metadataReader.GetString(ar.Name).StartsWith("Xcaciv.Command", StringComparison.OrdinalIgnoreCase))
                {
                    hasXcacivRef = true;
                    break;
                }
            }

            if (!hasXcacivRef)
            {
                return false;
            }

            foreach (var typeDefHandle in metadataReader.TypeDefinitions)
            {
                var typeDef = metadataReader.GetTypeDefinition(typeDefHandle);
                foreach (var interfaceImplHandle in typeDef.GetInterfaceImplementations())
                {
                    var interfaceImpl = metadataReader.GetInterfaceImplementation(interfaceImplHandle);
                    var interfaceName = GetInterfaceName(metadataReader, interfaceImpl.Interface);
                    if (interfaceName.Equals("ICommandDelegate", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static string GetInterfaceName(MetadataReader reader, EntityHandle handle)
    {
        if (handle.Kind == HandleKind.TypeReference)
        {
            var typeRef = reader.GetTypeReference((TypeReferenceHandle)handle);
            return reader.GetString(typeRef.Name);
        }
        if (handle.Kind == HandleKind.TypeDefinition)
        {
            var typeDef = reader.GetTypeDefinition((TypeDefinitionHandle)handle);
            return reader.GetString(typeDef.Name);
        }
        return String.Empty;
    }
}
