using DotJEM.SourceGen.SqlAdapterGenerator.Factories;
using DotJEM.SourceGen.SqlAdapterGenerator.Util;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Reflection.Metadata;
using System.Threading;

namespace DotJEM.SourceGen.SqlAdapterGenerator;

/// <summary>
/// Document My Framework Class.
/// </summary>
[Generator(LanguageNames.CSharp)]
public class SqlAdapterGenerator : IIncrementalGenerator
{
    // SEE: https://github.com/podimo/Podimo.ConstEmbed/blob/develop/src/Podimo.ConstEmbed/ConstEmbedGenerator.cs
    // SEE: https://stackoverflow.com/questions/72095200/c-sharp-incremental-generator-how-i-can-read-additional-files-additionaltexts
    // https://andrewlock.net/creating-a-source-generator-part-6-saving-source-generator-output-in-source-control/
    // https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.md

    private readonly AdapterGenerator generator = new();

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        //if (!Debugger.IsAttached)
        //    Debugger.Launch();

        IncrementalValueProvider<TemplateOptions> globalOptions = context.AnalyzerConfigOptionsProvider.Select((provider, token) =>
        {
            Debug.WriteLine("Collecting global options.");

            provider.GlobalOptions.TryGetValue($"build_property.RootNamespace", out string rootNamespace);
            provider.GlobalOptions.TryGetValue($"build_property.DotJEMSqlAdapterVisibility", out string defaultVisibility);
            provider.GlobalOptions.TryGetValue($"build_property.DotJEMSqlAdapterNamespace", out string defaultNamespace);
            if (string.IsNullOrWhiteSpace(defaultNamespace)) defaultNamespace = rootNamespace;
            return new TemplateOptions(defaultNamespace, defaultVisibility, null);
        });

        IncrementalValuesProvider<(AdditionalText text, TemplateOptions options)> templateFilesAndSettings = context.AdditionalTextsProvider
            .Where(text => text.Path.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Select(static (tuple, token) =>
            {
                Debug.WriteLine("Collecting additional texts: " + tuple.Left.Path);
                
                (AdditionalText text, AnalyzerConfigOptionsProvider provider) = tuple;
                AnalyzerConfigOptions options = provider.GetOptions(text);
                options.TryGetValue($"build_metadata.AdditionalFiles.Visibility", out string visibility);
                options.TryGetValue($"build_metadata.AdditionalFiles.Namespace", out string @namespace);
                return (text, new TemplateOptions(@namespace, visibility ?? "internal", PascalCaseTranform.Transform(Path.GetExtension(text.Path).Trim('.'))));
            });

        IncrementalValuesProvider<(AdditionalText text, TemplateOptions options)> merged = templateFilesAndSettings
            .Combine(globalOptions)
            .Select((tuple, token) =>
            {
                Debug.WriteLine("Combining: " + tuple.Left.text.Path);

                (AdditionalText text, TemplateOptions options) = tuple.Left;
                TemplateOptions defaults = tuple.Right;
                return (text, options: options | defaults);
            });

        IncrementalValuesProvider<string> templates = merged
            .Collect()
            .SelectMany((array, token) =>
            {
                List<string> files = new List<string>();
                foreach ((AdditionalText text, TemplateOptions options) in array)
                {
                    files.Add(text.Path);
                    generator.AddFile(text.Path, text.GetText(token)!.ToString(), options);
                }
                return files.ToArray();// generator.Generate();
            });
            //.Select((tuple, token) =>
            //{
                
            //    Debug.WriteLine("SelectMany: " + tuple.text.Path);
            //    generator.AddFile(tuple.text, tuple.options, token);
            //    return tuple.text.Path;
            //});

        //IncrementalValueProvider<ImmutableArray<string>> collected = templates
        //    .Collect()
        //    .Select();

        context.RegisterSourceOutput(templates, (spc, template) =>
        {
            Debug.WriteLine("RegisterSourceOutput: " + template);
            //spc.AddSource($"{template.Options.ClassName}.{template.Name}.{template.Key}.g.cs", template.ToString());
        });
    }
}
