using DotJEM.SourceGen.SqlAdapterGenerator.Util;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace DotJEM.SourceGen.SqlAdapterGenerator.Util;

public static class DictionaryExtensions
{
    extension(ImmutableDictionary<string, ImmutableArray<string>> self)
    {
        public string FirstOrDefault(string key) => self.TryGetValue(key, out ImmutableArray<string> value)
            ? value.FirstOrDefault()
            : null;
    }

    public static void AddVariables(this IDictionary<string, HashSet<string>> dictionary, string key, IEnumerable<string> values)
    {
        if (!dictionary.TryGetValue(key, out HashSet<string> set))
            dictionary.Add(key, set = new());
        foreach (string value in values)
            set.Add(value);
    }
}