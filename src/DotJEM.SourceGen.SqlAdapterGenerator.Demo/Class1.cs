using Microsoft.Data.SqlClient;
using System.Data;
using System.Data.Common;

namespace DotJEM.SourceGen.SqlAdapterGenerator.Demo
{
    public class DataTableAdapter(string schema, string dataTableName, SqlConnection connection)
    {
        

        public readonly record struct RecordName(Guid Id, string ContentType, long Version, DateTime Created, DateTime Updated, string CreatedBy, string UpdatedBy, string Data);
        /*
         *--method:Select<RecordName> --single:true
           SELECT [Id]
               ,[ContentType]
               ,[Version]
               ,[Created]
               ,[Updated]
               ,[CreatedBy]
               ,[UpdatedBy]
               ,[Data]
           FROM [@{schema}].[@{data_table_name}]
           WHERE [Id] = @id;
         *
         *
         */
        public RecordName Select(Guid id)
        {
            const string cmd = """
                               SELECT [Id]
                                   ,[ContentType]
                                   ,[Version]
                                   ,[Created]
                                   ,[Updated]
                                   ,[CreatedBy]
                                   ,[UpdatedBy]
                                   ,[Data]
                               FROM [@{schema}].[@{data_table_name}]
                               WHERE [Id] = @id;
                               """;

            SqlCommand command = new(cmd, connection);


            foreach ((string name, SqlDbType sqlDbType, object value) in parameters)
                command.Parameters.Add(name, sqlDbType).Value = value;
            return new SqlServerCommand(connection, command);


            SqlDataReader reader = null; // execute command and get reader 
            reader.


            return default;
        }

    }
}
