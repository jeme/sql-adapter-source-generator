using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DotJEM.SourceGen.SqlAdapterGenerator.Models;
using DotJEM.SourceGen.SqlAdapterGenerator.Util;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace DotJEM.SourceGen.SqlAdapterGenerator.Factories;

public class SqlFileReader
{
    public static List<SqlTemplateSpec> ReadAllSpecs(string content, string name, TemplateOptions options, Action<CreateTableStatement> addTableSpecCallback)
    {
        using StringReader reader = new StringReader(content);
        Dictionary<string, HashSet<string>> globals = new();
        SqlTemplateSpecBuilder[] specs = ReadToEnd(reader, globals).ToArray();
        return specs.Select(spec => spec.Build(globals, name, options, addTableSpecCallback)).ToList();
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
                Variables(line.AsSpan(3), globals.AddVariables);
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