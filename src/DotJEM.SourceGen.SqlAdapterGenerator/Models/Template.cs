using DotJEM.SourceGen.SqlAdapterGenerator.Util;

namespace DotJEM.SourceGen.SqlAdapterGenerator.Models;

public readonly record struct Template(TemplateOptions Options, string Name, string Key, ITemplatePart[] Parts);