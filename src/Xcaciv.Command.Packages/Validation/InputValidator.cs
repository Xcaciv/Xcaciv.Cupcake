namespace Xcaciv.Command.Packages.Validation;

using System;
using System.Text.RegularExpressions;

public class InputValidator
{
    private static readonly Regex PackageIdPattern = new(@"^[A-Za-z0-9_.-]+$", RegexOptions.Compiled);
    private static readonly Regex TermPattern = new(@"^[\p{L}0-9 _.:-]{1,100}$", RegexOptions.Compiled);

    public void ValidatePackageId(string packageId)
    {
        if (String.IsNullOrWhiteSpace(packageId) || !PackageIdPattern.IsMatch(packageId))
        {
            throw new ArgumentException("Invalid package id.", nameof(packageId));
        }
    }

    public void ValidateSearchTerms(string terms)
    {
        if (String.IsNullOrWhiteSpace(terms) || !TermPattern.IsMatch(terms))
        {
            throw new ArgumentException("Invalid search terms.", nameof(terms));
        }
    }
}
