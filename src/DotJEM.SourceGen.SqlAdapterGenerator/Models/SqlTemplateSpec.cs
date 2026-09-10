using DotJEM.SourceGen.SqlAdapterGenerator.Factories;
using DotJEM.SourceGen.SqlAdapterGenerator.Util;
using System.Collections.Immutable;

namespace DotJEM.SourceGen.SqlAdapterGenerator.Models;

public readonly record struct SqlTemplateSpec(
    string Name,
    Template Template,
    ImmutableDictionary<string, ImmutableArray<string>> Variables,
    ImmutableArray<string> Parameters)
{
    public string Spec => Variables.FirstOrDefault("spec");
    public string AdapterName => Variables.FirstOrDefault("adapter") ?? $"{Name}Adapter";
}