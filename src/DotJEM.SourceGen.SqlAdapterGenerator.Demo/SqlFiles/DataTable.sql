--spec: DataTable

--method:CreateTable
CREATE TABLE [@{schema}].[@{data_table_name}] (
    [Id] [uniqueidentifier] NOT NULL,
    [Version] [int] NOT NULL,
    [ContentType] [varchar](64) NOT NULL,
    [Created] [datetime] NOT NULL,
    [Updated] [datetime] NOT NULL,
    [CreatedBy] [nvarchar](256) NULL,
    [UpdatedBy] [nvarchar](256) NULL,
    [Header] [nvarchar](max) NOT NULL,
    [Data] [nvarchar](max) NOT NULL,
    [RV] [rowversion] NOT NULL,
    CONSTRAINT [PK_@{data_table_name}] PRIMARY KEY CLUSTERED ( [Id] ASC )
    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY];

ALTER TABLE [@{schema}].[@{data_table_name}] ADD CONSTRAINT [DF_@{data_table_name}_Id] DEFAULT (NEWSEQUENTIALID()) FOR [Id];

--method:Insert
INSERT INTO [@{schema}].[@{data_table_name}]
           ([ContentType]
           ,[Version]
           ,[Created]
           ,[Updated]
           ,[CreatedBy]
           ,[UpdatedBy]
           ,[Data])
     OUTPUT 'C', INSERTED.[Id], INSERTED.[Version], @timestamp, INSERTED.[CreatedBy], INSERTED.[Data] 
		INTO [@{schema}].[@{log_table_name}]([Event], [Id], [Version], [Time], [User], [Data])
     OUTPUT 
            INSERTED.[Id]
     VALUES
           (@contentType
           ,0
           ,@timestamp
           ,@timestamp
           ,@user
           ,@user
           ,@data);
           
--method:Select
  SELECT [Id]
      ,[ContentType]
      ,[Version]
      ,[Created]
      ,[Updated]
      ,[CreatedBy]
      ,[UpdatedBy]
      ,[Data]
      ,[RV]
  FROM [@{schema}].[@{data_table_name}]
  WHERE [Id] = @id;

--method:Select
SELECT [Id]
      ,[Version]
      ,[Created]
      ,[Updated]
      ,[CreatedBy]
      ,[UpdatedBy]
      ,[Data]
      ,[RV]
  FROM [@{schema}].[@{data_table_name}]
  ORDER BY [Created]
  OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY;
