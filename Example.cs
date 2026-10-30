using Microsoft.Data.Sqlite;




// TODO: inheritance of 
record Example: DbSqliteContext<Example>
{
    public record Table1: IDbSqliteContextTable
    {
        public record Text: IDbSqliteContextColumn
        {
            static public SqliteType DbType => SqliteType.Text;
        }
        public record Number: IDbSqliteContextColumn
        {
            static public SqliteType DbType => SqliteType.Integer;
        }
    }
}

record ExampleForeign: DbSqliteContext<ExampleForeign>
{
    public record Table1: IDbSqliteContextTable
    {
        public record Number: IDbSqliteContextColumn
        {
            static public SqliteType DbType => SqliteType.Integer;
        }
    }

    public record Table2: IDbSqliteContextTable
    {
        public record Reference: IDbSqliteContextColumnForeign<Table1>
        {}

        public record Value: IDbSqliteContextColumn
        {
            static public SqliteType DbType => SqliteType.Text;
        }
    }
}