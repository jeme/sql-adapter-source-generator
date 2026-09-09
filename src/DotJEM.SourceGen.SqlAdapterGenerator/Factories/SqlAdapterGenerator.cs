using DotJEM.SourceGen.SqlAdapterGenerator.Util;
using Microsoft.CodeAnalysis;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DotJEM.SourceGen.SqlAdapterGenerator.Factories;

public interface ITemplatePart;
public readonly record struct Template(TemplateOptions Options, string Name, string Key, ITemplatePart[] Parts);
public readonly record struct TemplateLiteral(string Source) : ITemplatePart;
public readonly record struct TemplateVariable(string Name) : ITemplatePart;

public static class StringTemplateFactory
{
    private static readonly Regex variablePattern = new Regex("@\\{(.+?)}", RegexOptions.Compiled);
    private static readonly Regex newLinePattern = new Regex(@"\r\n?|\n", RegexOptions.Compiled);

    public static Template Create(TemplateOptions options, string source, string name, string Key)
    {
        int index = 0;
        List<ITemplatePart> parts = new List<ITemplatePart>();
        source = newLinePattern.Replace(source, "\\n");
        foreach (Match match in variablePattern.Matches(source).Cast<Match>())
        {
            string key = match.Groups[1].Value;
            parts.Add(new TemplateLiteral(source.Substring(index, match.Index - index).Replace("\"", "\\\"")));
            parts.Add(new TemplateVariable(key));
            index = match.Index + key.Length + 3;
        }
        parts.Add(new TemplateLiteral(source.Substring(index).Replace("\"", "\\\"")));

        return new(options, name, Key, parts.ToArray());
    }
}

sealed class ParameterVisitor : TSqlFragmentVisitor
{
    public List<string> Parameters { get; } = new();

    public override void ExplicitVisit(VariableReference node)
    {
        Parameters.Add(node.Name);
    }
}

public class AdapterGenerator
{
    private List<SqlTemplateSpec> templates = new();
    private Dictionary<string, TableSpec> schemas = new();
    private Dictionary<string, List<SqlTemplateSpec>> adapters = new();


    public IEnumerable<AdapterOutput> Generate()
    {


        return [];
    }

    public void AddFile(string path, string content, TemplateOptions options)
    {
        string name = PascalCaseTranform.Transform(Path.GetFileNameWithoutExtension(path));

        templates = SqlFileReader.ReadAllSpecs(content, name, options).ToList();
        foreach (SqlTemplateSpec spec in templates)
        {
            if (!adapters.TryGetValue(spec.AdapterName, out List<SqlTemplateSpec> list))
                adapters.Add(spec.AdapterName, list = []);
            list.Add(spec);
        }

    }


    private void AddTableSpec(SqlTemplateSpec spec, CreateTableStatement statement)
    {
        string schemaName = statement.SchemaObjectName.SchemaIdentifier.Value;
        string tableName = statement.SchemaObjectName.BaseIdentifier.Value;
        IList<ColumnSpec> columns = statement.Definition.ColumnDefinitions
            .Select(def =>
            {
                string identifier = def.ColumnIdentifier.Value;
                string type = def.DataType.Name.BaseIdentifier.Value;
                return new ColumnSpec(identifier, type);
            }).ToArray();
        this.schemas.Add(spec.Spec, new TableSpec(schemaName, tableName, columns.ToImmutableArray()));
    }
}

public readonly record struct AdapterOutput
{
}

public record struct SqlTemplateVariables(ImmutableDictionary<string, ImmutableArray<string>> Variables)
{
    public static ImmutableDictionary<string, ImmutableArray<string>> From(IDictionary<string, HashSet<string>> dictionary)
    {
        return new SqlTemplateVariables(dictionary
            .ToDictionary(pair => pair.Key, pair => pair.Value.ToImmutableArray())
            .ToImmutableDictionary()).Variables;
    }
}

public static class DictionaryExtensions
{
    extension(ImmutableDictionary<string, ImmutableArray<string>> self)
    {
        public string FirstOrDefault(string key) => self.TryGetValue(key, out ImmutableArray<string> value)
             ? value.FirstOrDefault()
             : null;
    }

    public static void Add(this IDictionary<string, HashSet<string>> dictionary, string key, string value)
    {
        if (!dictionary.TryGetValue(key, out HashSet<string> set))
            dictionary.Add(key, set = new());
        set.Add(value);
    }
}


public readonly record struct SqlTemplateSpec(
    string Name,
    Template Template,
    ImmutableDictionary<string, ImmutableArray<string>> Variables,
    ImmutableArray<string> Parameters)
{
    public string Spec => Variables.FirstOrDefault("spec");
    public string AdapterName => Variables.FirstOrDefault("adapter") ?? $"{Name}Adapter";

}

public class SqlTemplateSpecBuilder
{
    private readonly StringBuilder content = new();
    private readonly Dictionary<string, HashSet<string>> variables = new();
    public void AppendLine(string line)
    {
        content.AppendLine(line);
    }
    public void AddVariable(string key, string[] values)
    {
        if (!variables.TryGetValue(key, out HashSet<string> set))
            variables.Add(key, set = new());

        foreach (var value in values)
            set.Add(value);
    }
    public SqlTemplateSpec Build(IDictionary<string, HashSet<string>> globals, string name, TemplateOptions options)
    {
        foreach (KeyValuePair<string, HashSet<string>> pair in globals)
            AddVariable(pair.Key, pair.Value.ToArray());

        string content = this.content.ToString();
        Template template = StringTemplateFactory.Create(options, content, "", "");

        TSqlParser parser = TSqlParser.CreateParser(SqlVersion.Sql170, false);
        TSqlScript script = (TSqlScript)parser.Parse(new StringReader(content), out IList<ParseError> errors);
        ParameterVisitor visitor = new();
        foreach (TSqlBatch batch in script.Batches)
        {
            foreach (TSqlStatement statement in batch.Statements)
            {
                statement.Accept(visitor);

                if (statement is CreateTableStatement createTableStatement)
                {
                    //TODO: Push out or???
                    //AddTableSpec(spec, createTableStatement);
                }



            }
        }
        return new SqlTemplateSpec(name, template, SqlTemplateVariables.From(variables), visitor.Parameters.ToImmutableArray());
    }

    public bool IsEmpty()
    {
        return content.Length > 0;
    }
}
public class SqlFileReader
{
    public static List<SqlTemplateSpec> ReadAllSpecs(string content, string name, TemplateOptions options)
    {
        using StringReader reader = new StringReader(content);
        Dictionary<string, HashSet<string>> globals = new();
        SqlTemplateSpecBuilder[] specs = ReadToEnd(reader, globals).ToArray();
        return specs.Select(spec => spec.Build(globals, name, options)).ToList();
    }

    public static IEnumerable<SqlTemplateSpecBuilder> ReadToEnd(StringReader reader, Dictionary<string, HashSet<string>> globals)
    {
        SqlTemplateSpecBuilder builder = new SqlTemplateSpecBuilder();
        bool capturingHeader = false;
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0)
                continue;

            if (line.StartsWith("--#"))
            {
                Variables(line.AsSpan(3), (key, values) =>
                {
                    builder.AddVariable(key, values);
                });
                capturingHeader = true;
                continue;
            }

            if (line.StartsWith("--"))
            {
                if (!capturingHeader)
                {
                    if (!builder.IsEmpty()) yield return builder;
                    builder = new SqlTemplateSpecBuilder();
                }

                Variables(line.AsSpan(2), (key, values) =>
                {
                    builder.AddVariable(key, values);
                });
                capturingHeader = true;
                continue;
            }

            capturingHeader = false;
            builder.AppendLine(line);
        }
        yield return builder;
    }

    static void Variables(ReadOnlySpan<char> line, Action<string, string[]> onVariable)
    {
        int position = 0;

        while (position < line.Length)
        {
            SkipWhitespace(line, ref position);

            if (position >= line.Length)
                return;

            // Optional "--" prefix
            if (position + 1 < line.Length &&
                line[position] == '-' &&
                line[position + 1] == '-')
            {
                position += 2;
            }


            int keyStart = position;

            while (position < line.Length &&
                   line[position] != ':' &&
                   !char.IsWhiteSpace(line[position]))
            {
                position++;
            }

            if (position >= line.Length || line[position] != ':')
                throw new FormatException($"Expected ':' at position {position}.");

            string key = line.Slice(keyStart, position - keyStart).ToString();

            position++; // Skip ':'
            SkipWhitespace(line, ref position);

            string[] values;

            if (position < line.Length && line[position] == '[')
            {
                position++; // Skip '['

                // First pass: count values so the array has the exact size.
                int valuesStart = position;
                int countPosition = position;
                int count = 0;

                while (true)
                {
                    SkipWhitespace(line, ref countPosition);

                    if (countPosition >= line.Length)
                        throw new FormatException($"Unclosed list for '{key}'.");

                    if (line[countPosition] == ']')
                        break;

                    SkipValue(line, ref countPosition);
                    count++;
                }

                // Second pass: parse directly into the final array.
                values = new string[count];
                position = valuesStart;

                for (int i = 0; i < count; i++)
                {
                    SkipWhitespace(line, ref position);
                    values[i] = ReadValue(line, ref position);
                }

                SkipWhitespace(line, ref position);

                if (position >= line.Length || line[position] != ']')
                    throw new FormatException($"Unclosed list for '{key}'.");

                position++; // Skip ']'
            }
            else
            {
                values = new[] { ReadValue(line, ref position) };
            }

            onVariable(key, values);
        }
    }

    static string ReadValue(ReadOnlySpan<char> text, ref int position)
    {
        if (position >= text.Length)
            throw new FormatException("Expected a value.");

        if (text[position] == '"')
        {
            int start = ++position;
            while (position < text.Length && text[position] != '"')
                position++;

            if (position >= text.Length)
                throw new FormatException("Unclosed quoted value.");

            string value = text.Slice(start, position - start).ToString();
            position++; // Skip closing quote

            return value;
        }

        int valueStart = position;

        while (position < text.Length &&
               !char.IsWhiteSpace(text[position]) &&
               text[position] != ']')
        {
            position++;
        }

        if (position == valueStart)
            throw new FormatException($"Expected a value at position {position}.");

        return text.Slice(valueStart, position - valueStart).ToString();
    }

    static void SkipValue(ReadOnlySpan<char> text, ref int position)
    {
        if (text[position] == '"')
        {
            position++;

            while (position < text.Length && text[position] != '"')
                position++;

            if (position >= text.Length)
                throw new FormatException("Unclosed quoted value.");

            position++;
            return;
        }

        int start = position;

        while (position < text.Length &&
               !char.IsWhiteSpace(text[position]) &&
               text[position] != ']')
        {
            position++;
        }

        if (position == start)
            throw new FormatException($"Expected a value at position {position}.");
    }

    static void SkipWhitespace(ReadOnlySpan<char> text, ref int position)
    {
        while (position < text.Length &&
               char.IsWhiteSpace(text[position]))
        {
            position++;
        }
    }
}


public readonly record struct ColumnSpec(string Name, string Type);

public readonly record struct TableSpec(string Schema, string Name, ImmutableArray<ColumnSpec> Columns);
//public class TableSpecReader
//{
//    public IEnumerable<SqlTemplateSpec> ReadToEnd(StringReader reader)
//    {
//        StringBuilder buffer = new();
//        string definition = null;
//        while (reader.ReadLine() is { } line)
//        {
//            if (line.StartsWith("--"))
//            {
//                if (buffer.Length > 0)
//                {
//                    yield return new(buffer.ToString(), definition);
//                    buffer.Clear();
//                }
//                definition = line;
//                continue;
//            }
//            buffer.AppendLine(line);
//        }

//        if (buffer.Length > 0)
//            yield return new(buffer.ToString(), definition);
//    }
//}