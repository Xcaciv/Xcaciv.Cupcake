using System;
using System.Collections.Generic;

namespace Xcaciv.Command.Packages.Services;

public class SecurityPolicyService
{
    private readonly bool allowExternalDependencies;

    public SecurityPolicyService(bool allowExternalDependencies = false)
    {
        this.allowExternalDependencies = allowExternalDependencies;
    }

    public void EnforcePolicyOnDependencies(IReadOnlyList<string> externalDependencies)
    {
        if (externalDependencies is null || externalDependencies.Count == 0)
        {
            return;
        }

        if (!this.allowExternalDependencies)
        {
            throw new InvalidOperationException(
                $"Installation rejected: Package declares external NuGet dependencies but policy AllowExternalDependencies is false. " +
                $"Dependencies: {String.Join(", ", externalDependencies)}");
        }
    }

    public void LogPolicyViolation(string packageId, string reason)
    {
        // Structured logging hooks for security audit trail
        // In production, this would log to a centralized security event log
    }
}
