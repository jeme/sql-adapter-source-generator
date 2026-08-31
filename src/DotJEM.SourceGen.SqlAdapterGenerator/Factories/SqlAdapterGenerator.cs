using DotJEM.SourceGen.SqlAdapterGenerator.Util;
using Microsoft.CodeAnalysis;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading;

namespace DotJEM.SourceGen.SqlAdapterGenerator.Factories;

public class AdapterGenerator
{
    public TableSpec Generate(string source)
    {
        //IEnumerable<(string Key, string Template)> parts = new TableSpecReader().ReadToEnd(new StringReader(source)):

        TSqlParser parser = TSqlParser.CreateParser(SqlVersion.Sql170, false);
        TSqlScript script = (TSqlScript)parser.Parse(new StringReader(source), out IList<ParseError> errors);

        TSqlBatch first = script.Batches.First();
        CreateTableStatement createTable = first.Statements.First() as CreateTableStatement;

        string schemaName = createTable.SchemaObjectName.SchemaIdentifier.Value;
        string tableName = createTable.SchemaObjectName.BaseIdentifier.Value;
        IList<ColumnSpec> columns = createTable.Definition.ColumnDefinitions
            .Select(def =>
            {
                string identifier = def.ColumnIdentifier.Value;
                string type = def.DataType.Name.BaseIdentifier.Value;
                return new ColumnSpec(identifier, type);
            }).ToArray();


        return new TableSpec();
    }

    public void Generate()
    {



    }

    public void AddFile(AdditionalText content, TemplateOptions tupleOptions, CancellationToken token)
    {
        string name = PascalCaseTranform.Transform(Path.GetFileNameWithoutExtension(content.Path));
        string sourceFromFile = content.GetText(token)!.ToString();

        //new SqlFileReader(sourceFromFile);
        IEnumerable<SqlTemplateSpec> specs = SqlFileReader.ReadToEnd(new StringReader(sourceFromFile));
        foreach (SqlTemplateSpec spec in specs)
        {

        }


    }
}

public record SqlTemplateVariables(ImmutableDictionary<string, ImmutableArray<string>> variables)
{
    
    public static SqlTemplateVariables From(IDictionary<string, HashSet<string>> dictionary)
    {
        return new SqlTemplateVariables(dictionary
            .ToDictionary(pair => pair.Key, pair => pair.Value.ToImmutableArray())
            .ToImmutableDictionary());
    }
}

public readonly record struct SqlTemplateSpec(string Content, SqlTemplateVariables Variables);
public class SqlFileReader
{
    public static IEnumerable<SqlTemplateSpec> ReadToEnd(StringReader reader)
    {
        StringBuilder buffer = new();

        Dictionary<string, HashSet<string>> variables = new();
        bool capturingHeader = false;
        while (reader.ReadLine() is { } line)
        {
            if (line.StartsWith("--"))
            {
                if (!capturingHeader)
                {
                    yield return new SqlTemplateSpec(buffer.ToString(), SqlTemplateVariables.From(variables));
                    buffer.Clear();
                    variables.Clear();
                }

                Variables(line, (key, values) =>
                {
                    if (!variables.TryGetValue(key, out HashSet<string> set))
                        variables.Add(key, set = new());

                    foreach (var value in values)
                        set.Add(value);
                });
                capturingHeader = true;
                continue;
            }

            capturingHeader = false;
            buffer.AppendLine(line);
        }
        yield return new SqlTemplateSpec(buffer.ToString(), SqlTemplateVariables.From(variables));
    }

    //private static IEnumerable<SqlTemplateVariable> Variables(string line, Action<string, string[]> addValue)
    //{
    //}

    static void Variables(string line, Action<string, string[]> onVariable)
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

            string key = line.Substring(keyStart, position - keyStart);

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

    static string ReadValue(string text, ref int position)
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

            string value = text.Substring(start, position - start);
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

        return text.Substring(valueStart, position - valueStart);
    }

    static void SkipValue(string text, ref int position)
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

    static void SkipWhitespace(string text, ref int position)
    {
        while (position < text.Length &&
               char.IsWhiteSpace(text[position]))
        {
            position++;
        }
    }
}
public readonly record struct ColumnSpec(string Name, string Type);
public readonly record struct TableSpec(string Schema, string Name, ColumnSpec[] Columns)
{

}


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