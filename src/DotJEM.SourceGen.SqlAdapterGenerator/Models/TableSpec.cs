using System.Collections.Immutable;

namespace DotJEM.SourceGen.SqlAdapterGenerator.Models;

public readonly record struct TableSpec(string Schema, string Name, ImmutableArray<ColumnSpec> Columns);