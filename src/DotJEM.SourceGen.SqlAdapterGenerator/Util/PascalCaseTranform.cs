using System.Text.RegularExpressions;

namespace DotJEM.SourceGen.SqlAdapterGenerator.Util;


public static class PascalCaseTranform
{

    private static readonly Regex pascalCaseFilter = new Regex("^\\w|\\-\\w", RegexOptions.Compiled);
    public static string Transform(string fileExtension)
    {
        return $"{pascalCaseFilter.Replace(fileExtension, match => match.Index == 0
            ? match.ToString().ToUpperInvariant()
            : match.ToString().ToUpperInvariant().Substring(1, 1))}Templates";
    }
}