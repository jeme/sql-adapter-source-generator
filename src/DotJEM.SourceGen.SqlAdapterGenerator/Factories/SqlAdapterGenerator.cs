using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DotJEM.SourceGen.SqlAdapterGenerator.Factories;

public class SqlAdapterGeneratorFactory
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
            .Select(def => {
                string identifier = def.ColumnIdentifier.Value;
                string type = def.DataType.Name.BaseIdentifier.Value;
                return new ColumnSpec(identifier, type);
            }).ToArray();


        return new TableSpec();
    }
}


public readonly record struct ColumnSpec(string Name, string Type);
public readonly record struct TableSpec(string Schema, string Name, ColumnSpec[] Columns)
{

}

public readonly record struct Spec(string Content, string Header);

public class TableSpecReader
{
    public IEnumerable<Spec> ReadToEnd(StringReader reader)
    {
        StringBuilder buffer = new();
        string definition = null;
        while (reader.ReadLine() is { } line)
        {
            if (line.StartsWith("--"))
            {
                if (buffer.Length > 0)
                {
                    yield return new (buffer.ToString(), definition);
                    buffer.Clear();
                }
                definition = line;
                continue;
            }
            buffer.AppendLine(line);
        }

        if (buffer.Length > 0)
            yield return new(buffer.ToString(), definition);
    }
}