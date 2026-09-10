using DotJEM.SourceGen.SqlAdapterGenerator.Models;
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
    private readonly Dictionary<string, TableSpec> schemas = new();
    private readonly Dictionary<string, List<SqlTemplateSpec>> adapters = new();


    public IEnumerable<AdapterOutput> Generate()
    {
        foreach (KeyValuePair<string, List<SqlTemplateSpec>> pair in adapters)
        {
            foreach (SqlTemplateSpec spec in pair.Value)
            {
                
            }
        }

        return [];
    }

    public void AddFile(string path, string content, TemplateOptions options)
    {
        string name = PascalCaseTranform.Transform(Path.GetFileNameWithoutExtension(path));

        List<SqlTemplateSpec> templates = SqlFileReader
            .ReadAllSpecs(content, name, options, AddTableSpec)
            .ToList();
        foreach (SqlTemplateSpec spec in templates)
        {
            if (!adapters.TryGetValue(spec.AdapterName, out List<SqlTemplateSpec> list))
                adapters.Add(spec.AdapterName, list = []);
            list.Add(spec);
        }

    }


    private void AddTableSpec(CreateTableStatement statement)
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
        this.schemas.Add($"{schemaName}.{tableName}", new TableSpec(schemaName, tableName, columns.ToImmutableArray()));
    }
}

public readonly record struct AdapterOutput
{
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
    public SqlTemplateSpec Build(IDictionary<string, HashSet<string>> globals, string name, TemplateOptions options, Action<CreateTableStatement> addTableSpecCallback)
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
                    addTableSpecCallback(createTableStatement);
                }

                if (statement is SelectStatement selectStatement)
                {

                }

                if (statement is UpdateStatement updateStatement)
                {

                }
                

                if (statement is InsertStatement insertStatement)
                {

                }
            }
        }
        return new SqlTemplateSpec(name, template, SqlTemplateVariables.From(variables), visitor.Parameters.ToImmutableArray());
    }

    public bool IsEmpty()
    {
        return content.Length == 0;
    }
}