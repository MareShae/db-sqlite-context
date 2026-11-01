using Microsoft.Data.Sqlite;




// TODO: inheritance of 
record Example: DbSqliteContext<Example>
{
    [DbSqliteContextTable]
    public record Table1
    {
        [DbSqliteContextColumn]
        static public string? Text;

        [DbSqliteContextColumn]
        static public int? Number;
    }

    [DbSqliteContextTable]
    public record Table2
    {
        [DbSqliteContextColumn]
        [DbSqliteContextForeign<Table1>]
        static public int Reference;

        [DbSqliteContextColumn]
        static public string? Randstr;
    }
}