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