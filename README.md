# Sqlite Database Context

Sqlite Database Context (DbSqliteContext) is a copy-and-paste solution that helps to structure sqlite databases from the cs file.

The top-most, which represents the Sqlite connection, is a `record` that inherits from the *DbSqliteContext* generic class.
The name of the record is automatically used as the source of the Sqlite connection.

Similarly, tables are records, and tables and columns are created in the database as they are named in the file.

Tables and columns are indicated by including the *DbSqliteContextTable* and *DbSqliteContextColumn* attributes respectively.

The *DbSqliteContextForeign* generic attribute is used specifically for columns that reference data within the same Sqlite connection.

The data type of the column is indicated within its associated table and declared as a standard variable.

`Tables` must be marked as public to be used as intended (see Example)
`Columns` must be static and public.

IDE like VSCode might flag non-nullable fields. Use nullable fields.
