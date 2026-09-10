using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace DotJEM.SourceGen.SqlAdapterGenerator.Models;

public record struct SqlTemplateVariables(ImmutableDictionary<string, ImmutableArray<string>> Variables)
{
    public static ImmutableDictionary<string, ImmutableArray<string>> From(IDictionary<string, HashSet<string>> dictionary)
    {
        return new SqlTemplateVariables(dictionary
            .ToDictionary(pair => pair.Key, pair => pair.Value.ToImmutableArray())
            .ToImmutableDictionary()).Variables;
    }
}