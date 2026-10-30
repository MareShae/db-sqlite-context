using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

interface IDbSqliteContext
{
    static SqliteConnection? __SqliteConnection {get;}
    static public JsonObject? __JsonStructure  {get; }
}

interface IDbSqliteContextTable {}

interface IDbSqliteContextColumn
{
    static public SqliteType DbType {get;}
}



/** implement Architecture as base class for inhertance **/

record DbSqliteContext<TChild>: IDbSqliteContext
{
    /* these are constants for JsonObject fields */
    static readonly string TYPE = "TYPE";
    static readonly string PROPERTIES = "PROPERTIES";
    static readonly string FIELDS = "FIELDS";
    static readonly string NESTEDTYPES = "NESTEDTYPES";

    /* special column */
    static readonly string Id = "Id";


    static public SqliteConnection? __SqliteConnection {
        private set
        {
            field = value;
        }
        get
        {
            if (field is null)
            {
                string Source = $"{typeof(TChild).Name}.db";
                field = new SqliteConnection (
                    $"Data Source={Source}; Foreign Keys=True"
                );
                field.Open();
            }
            return field;
        }
    }
    static public JsonObject? __JsonStructure  {
        private set
        {
            field = value;
        }
        get
        {
            if (field is null) {
                field = new JsonObject();

                Type type = typeof(TChild);
                field[type.Name] = Structure(type);
            }

            return field;
        }
    }
    


    /// <summary>
    /// checks if the table is in the database
    /// </summary>
    /// 
    /// <param name="Table"></param>
    /// 
    /// <returns></returns>
    static private bool IsInDatabase(string Table)
    {
        if (__SqliteConnection is null) return false;
        
        using var command = __SqliteConnection.CreateCommand();
        command.CommandText = $"""
        SELECT name
        FROM sqlite_master
        WHERE type='table' AND name='{Table}'
        """;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            return true;
        }
        
        return false;
    }


    /// <summary>
    /// checks if a column is under the associated table in the database
    /// </summary>
    /// 
    /// <param name="Table"></param>
    /// <param name="Column"></param>
    /// 
    /// <returns></returns>
    static private (bool _Name, bool _Type)? IsInDatabase(string Table, string Column)
    {
        // check if the Table/Column exist in the Structure
        if (FetchFromStructure(Table, Column) is null) return null;
        // check if the Table exists in Database
        if (! IsInDatabase(Table)) return null;

        if (__SqliteConnection is null) return null;

        using var command = __SqliteConnection.CreateCommand();

        command.CommandText = $"""
        PRAGMA table_info({Table})
        """;

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (Column == reader.GetString(1)) {
                return (
                    true,   // true for column name
                    SqliteTypeOf(Table, Column).ToString()!.ToLower() == reader.GetString(2).ToLower()
                );
            }
        }

        return null;
    }


    /// <summary>
    /// prints all table and their data to the console
    /// </summary>
    static public void ShowAllTableInDatabase()
    {
        if (__SqliteConnection is null) return;

        // get the table names in the database
        
        using var commandI = __SqliteConnection.CreateCommand();
        commandI.CommandText = $"""
        SELECT name
        FROM sqlite_master
        WHERE type='table' AND name NOT LIKE 'sqlite%'
        """;

        using var readerI = commandI.ExecuteReader();
        while (readerI.Read())
        {
            // get the name of the table
            string table = readerI.GetString(0);

            // get the names of the column
            List<string> columns = new(); {
                using var commandII = __SqliteConnection.CreateCommand();
                commandII.CommandText = $"""
                PRAGMA table_info({table})
                """;

                using var readerII = commandII.ExecuteReader();
                while (readerII.Read())
                {
                    columns.Add(readerII.GetString(1));
                }
            }

            // print the table
            using var commandIII = __SqliteConnection.CreateCommand();
            commandIII.CommandText = $"""
            SELECT {string.Join(", ", columns)}
            FROM {table}
            """;

            Console.WriteLine($"<Table: {table}>");
            using var readerIII = commandIII.ExecuteReader();
            while (readerIII.Read())
            {
                for (int i = 0; i < columns.Count; i++) {
                    SqliteType? sqliteType = SqliteTypeOf(table, columns[i]);
                    if (sqliteType is null) break;

                    Console.Write($"\t{columns[i]}: ");
                    switch(sqliteType)
                    {
                        case SqliteType.Integer: {
                            Console.WriteLine(readerIII.GetInt64(i));
                            break;
                        }
                        case SqliteType.Real: {
                            Console.WriteLine(readerIII.GetDouble(i));
                            break;
                        }
                        case SqliteType.Text: {
                            Console.WriteLine(readerIII.GetString(i));
                            break;
                        }
                        case SqliteType.Blob: {
                            Console.WriteLine(readerIII.GetValue(i));
                            break;
                        }
                        default: {
                            Console.WriteLine("");
                            break;
                        }
                    }
                }
                Console.WriteLine($"-------");
            }
            Console.WriteLine($"</Table: {table}>");
        }
    }


    /// <summary>
    /// get just the names of the tables
    /// </summary>
    /// <returns></returns>
    static public string[] GetTableInStructure()
    {

        var json = FetchFromStructure();

        // check if it has the NESTEDTYPES container
        if (! json.ContainsKey(NESTEDTYPES)) {
            throw new KeyNotFoundException(NESTEDTYPES);
        }

        // enter the NESTEDTYPES container
        json = (JsonObject) json[NESTEDTYPES]!;

        return json.Select(p => p.Key).ToArray();
    }


    /// <summary>
    /// get the names of the columns of an associated table
    /// </summary>
    /// <param name="Table"></param>
    /// <returns></returns>
    static public string[] GetColumnInStructure(string Table)
    {
        var json = FetchFromStructure(Table);

        // check if ${table} has the NESTEDTYPES container
        if (! json.ContainsKey(NESTEDTYPES)) {
            throw new KeyNotFoundException(NESTEDTYPES);
        }

        // enter the NESTEDTYPES container
        json = (JsonObject) json[NESTEDTYPES]!;

        return json.Select(p => p.Key).ToArray();
    }


    /// <summary>
    /// creates the table in the database, if it does not exist
    /// it is created in a `default` mode, with only Id in the column
    /// columns are subsequently included when they are accessed
    /// </summary>
    /// 
    /// <param name="Table"></param>
    /// 
    /// <returns></returns>
    static private bool MirrorToDatabase(string Table)
    {
        if (__SqliteConnection is null) return false;

        // nothing to do if it is already in the database
        // return true to show it exists
        if (IsInDatabase(Table)) return true;

        // check if it is in the Structure
        // returns false because the Structure is the blueprint
        if (FetchFromStructure(Table) is null) return false;

        using var command = __SqliteConnection.CreateCommand();

        // create only a base table
        command.CommandText = $"""
        CREATE TABLE IF NOT EXISTS {Table}
        (Id INTEGER PRIMARY KEY AUTOINCREMENT)
        STRICT
        """;

        command.ExecuteNonQuery();

        return true; 
    }


    /// <summary>
    /// creates a column for the associated table, if it does not exist
    /// </summary>
    /// 
    /// <param name="Table"></param>
    /// <param name="Column"></param>
    /// 
    /// <returns></returns>
    static private bool MirrorToDatabase(string Table, string Column)
    {   
        if (__SqliteConnection is null) return false;

        // nothing to do if the Table/Column exists in Database
        var IsInDB = IsInDatabase(Table, Column);
        if (IsInDB is not null && IsInDB.Value._Name && IsInDB.Value._Type) return true;

        // check if the Table/Column exist in the Structure
        if (FetchFromStructure(Table, Column) is null) return false;
        // ensure the Table exists in the Database
        MirrorToDatabase(Table);

        /**
         * 1. table does not exist
         * 2. column does not exist
        **/
        if (IsInDB is null)
        {
            // create the Column in the Table
            using var command = __SqliteConnection.CreateCommand();

            var dbType = SqliteTypeOf(Table, Column);

            // create only a base table
            command.CommandText = $"""
            ALTER TABLE {Table}
            ADD COLUMN {Column} {dbType} NOT NULL
            DEFAULT {DefaultValueOfSqliteType((SqliteType)dbType)}
            """;

            command.ExecuteNonQuery();

            return true;
        }
        /**
         * the name exists but the data type has changed
        **/
        else if (IsInDB.Value._Name && !IsInDB.Value._Type)
        {
            // TODO
        }

        return false;
    }


    /// <summary>
    /// extract the database structure that implement
    ///     + IDbSqliteContext
    ///     + IDbSqliteContextTable
    ///     + IDbSqliteContextColumn
    /// </summary>
    /// 
    /// <param name="type"></param>
    /// 
    /// <returns></returns>
    static private JsonObject? Structure(Type type)
    {
        var json = new JsonObject();
        
        json[TYPE] = ExtractType(type);
        if ((string) json[TYPE]! == string.Empty) return null;

        json[TYPE] = ExtractType(type);
        json[PROPERTIES] = ExtractProperties(type);
        json[FIELDS] = ExtractFields(type);
        json[NESTEDTYPES] = ExtractNestedTypes(type);

        return json;
    }


    /// <summary>
    /// checks only for one of the accepted 
    ///     + IDbSqliteContext
    ///     + IDbSqliteContextTable
    ///     + IDbSqliteContextColumn
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    static private string ExtractType(Type type)
    {
        if (type.GetInterface(nameof(IDbSqliteContext)) is not null)
        {
            return typeof(IDbSqliteContext).Name;
        }

        if (type.GetInterface(nameof(IDbSqliteContextTable)) is not null)
        {
            return typeof(IDbSqliteContextTable).Name;
        }

        if (type.GetInterface(nameof(IDbSqliteContextColumn)) is not null)
        {
            return typeof(IDbSqliteContextColumn).Name;
        }

        return string.Empty;
    }


    /// <summary>
    /// extracts the properties of the class
    /// </summary>
    /// 
    /// <param name="type"></param>
    /// 
    /// <returns></returns>
    static private JsonObject ExtractProperties(Type type)
    {
        var json = new JsonObject();

        foreach (var p in type.GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.Instance))
        {
            json[p.Name] = JsonSerializer.SerializeToNode(p.GetValue(null)!);
        }

        return json;
    }


    /// <summary>
    /// extracts the fields of the class
    /// </summary>
    /// 
    /// <param name="type"></param>
    /// 
    /// <returns></returns>
    static private JsonObject ExtractFields(Type type)
    {
        var json = new JsonObject();

        foreach (var f in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.Instance))
        {
            json[f.Name] = JsonSerializer.SerializeToNode(f.GetValue(null)!);
        }

        return json;
    }


    /// <summary>
    /// extracts nested (in this case) records
    /// which represent either Tables or Columns
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    static private JsonObject ExtractNestedTypes(Type type)
    {
        var json = new JsonObject();

        foreach (var n in type.GetNestedTypes(BindingFlags.Public | BindingFlags.Instance))
        {
            var tmp = Structure(n);
            if (tmp is not null) {
                json[n.Name] = Structure(n);
            }
        }
        return json;
    }

    
    /// <summary>
    /// a wrapper around fetching the main structure for the database
    /// </summary>
    /// 
    /// <returns></returns>
    static private JsonObject FetchFromStructure()
    {
        // __JsonStructure should auto populate itself, so it would not be null
        if (__JsonStructure is null) {
            throw new ArgumentException(nameof(__JsonStructure), $"uninitialized");
        }

        var json = __JsonStructure;

        // check if it has the NESTEDTYPES container
        string selfName = typeof(TChild).Name;
        if (! json.ContainsKey(selfName)) throw new KeyNotFoundException(selfName);
        // enter the SELF container
        json = (JsonObject) json[selfName]!;

        if (json is not null)
        {
            return json;
        }

        // enter the structure
        throw new ArgumentNullException(nameof(json));
    }
    
    /// <summary>
    /// a wrapper around getting the structure of a particular table
    /// it contains just the columns and the table metadata
    /// </summary>
    /// 
    /// <param name="table"></param>
    /// <returns></returns>
    static private JsonObject FetchFromStructure(string table)
    {
        var json = FetchFromStructure();

        // check if it has the NESTEDTYPES container
        if (! json.ContainsKey(NESTEDTYPES)) {
            throw new KeyNotFoundException(NESTEDTYPES);
        }
        // enter the NESTEDTYPES container
        json = (JsonObject) json[NESTEDTYPES]!;

        // check if the ${table} is one of its keys
        if (! json.ContainsKey(table))
        {
            throw new KeyNotFoundException(table);
        }
        // enter the ${table}
        json = (JsonObject) json[table]!;

        // check if TYPE is defined
        if (! json.ContainsKey(TYPE)) {
            throw new KeyNotFoundException(TYPE);
        }

        // check if ${table} is truely a IDbSqliteContextTable
        if ((string)json[TYPE]! == nameof(IDbSqliteContextTable))
        {
            return json;
        }
        throw new ArgumentException(nameof(json), $"type is not {nameof(IDbSqliteContextTable)}");
    }


    /// <summary>
    /// a wrapper around accessing the structure of a specific column
    /// </summary>
    /// 
    /// <param name="table"></param>
    /// <param name="column"></param>
    /// 
    /// <returns></returns>
    static private JsonObject FetchFromStructure(string table, string column)
    {
        var json = FetchFromStructure(table);

        // check if ${table} has the NESTEDTYPES container
        if (! json.ContainsKey(NESTEDTYPES)) throw new KeyNotFoundException(NESTEDTYPES);
        // enter the NESTEDTYPES container
        json = (JsonObject) json[NESTEDTYPES]!;

        /* special case */
        if (column == Id) return new JsonObject();

        // check if the ${column} is one of its keys
        if (! json.ContainsKey(column)) throw new KeyNotFoundException(column);
        // enter the ${column}
        json = (JsonObject) json[column]!;

        // check if TYPE is defined
        if (! json.ContainsKey(TYPE)) throw new KeyNotFoundException(TYPE);

        // check if ${column} is truely a IDbSqliteContextColumn
        if ((string)json[TYPE]! == nameof(IDbSqliteContextColumn))
        {
            return json;
        }

        throw new ArgumentException(nameof(json), $"type is not {nameof(IDbSqliteContextColumn)}");
    }


    /// <summary>
    /// a wrapper around getting the SqliteType of a specific column
    /// </summary>
    /// <param name="table"></param>
    /// <param name="column"></param>
    /// <returns></returns>
    static private SqliteType SqliteTypeOf(string table, string column)
    {
        /* special case */
        if (column == Id) return SqliteType.Integer;

        var json = FetchFromStructure(table, column);

        // check if PROPERTIES is defined
        if (! json.ContainsKey(PROPERTIES)) throw new KeyNotFoundException(PROPERTIES);
        // enter the PROPERTIES
        json = (JsonObject) json[PROPERTIES]!;

        // check if DbTypes is defined
        string targetName = nameof(IDbSqliteContextColumn.DbType);
        if (! json.ContainsKey(targetName)) throw new KeyNotFoundException(targetName);
        // enter the target

        return JsonSerializer.Deserialize<SqliteType>(json[targetName]!);
    }


    /// <summary>
    /// returns a default value for a given sqlite type
    /// </summary>
    /// 
    /// <param name="sqliteType"></param>
    /// 
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    static private string DefaultValueOfSqliteType(SqliteType sqliteType)
    {
        switch (sqliteType)
        {
            case SqliteType.Integer: {
                return "0";
            }
            case SqliteType.Real: {
                return "0";
            }
            case SqliteType.Text: {
                return "''";
            }
            case SqliteType.Blob: {
                return (new byte[1]).ToString()!;
            }
            default: 
                throw new Exception($"no default on {sqliteType}");
        }
    }



    /// <summary>
    /// this class is specifically for executing Sqlite Commands
    /// 
    /// it uses a template that takes a IDbSqliteContextTable
    /// and subsequent methods take IDbSqliteContextColumn
    /// 
    /// it is by design to reduce errors for Sqlite Commands that
    /// are not checked during compilation
    /// 
    /// most of the functions return the Action object (deliberate)
    /// so that most Executions can take place without assigning to a variable
    /// </summary>
    /// 
    /// <typeparam name="T"></typeparam>
    public class Action<T>
    where T: IDbSqliteContextTable
    {
        // associated table
        private string __Table;
        // sqlite command for execution in the action
        private SqliteCommand __SqliteCommand;
        // used only when reading out a value from execution
        // it switches from ExecuteNoQuery to ExecuteReader
        private string[]? __Readout;
        // used to prevent Parse, Format, Parameter before Command
        private bool PreventInjectionBeforeCommandFlag;
        // used to prevent Readout after Parse, Format, Parameter
        private bool PreventReadoutAfterInjectionFlag;


        public Action()
        {
            // ensure the sqliteconnection is active
            if (__SqliteConnection is null) {
                throw new ArgumentException(nameof(__SqliteConnection), $"no connection for this action");
            }

            // get the name of the Table
            string Table = typeof(T).Name;

            // ensure the Table is in the database
            if (! MirrorToDatabase(Table!)) {
                throw new ArgumentException(nameof(Table), "mirror failed / action useless");
            }

            // associate the table
            __Table = Table;

            __SqliteCommand = __SqliteConnection.CreateCommand();

            /* flag defaults */
            PreventInjectionBeforeCommandFlag = true;
            PreventReadoutAfterInjectionFlag = false;
        }

        ~Action ()
        {
            __SqliteCommand.Dispose();
        }

        /// <summary>
        /// must be run before calling any of Parse.., Fprmat, Parameter
        /// 
        /// it populates the Command Text of the Action's SqliteCommand
        /// </summary>
        /// 
        /// <param name="Text"></param>
        /// 
        /// <returns></returns>
        public Action<T> Command(string Text)
        {
            __SqliteCommand.CommandText = Text
                .Replace("#Table", __Table);

            // allow injection now that command text has been set
            PreventInjectionBeforeCommandFlag = false;

            return this;
        }

        /// <summary>
        /// must be run before calling any of Parse.., Fprmat, Parameter
        /// 
        /// it is both a flag and a reference
        /// 
        /// when assigned, it switches Execution to ExecuteReader
        /// </summary>
        /// <param name="readOut"></param>
        /// <returns></returns>
        public Action<T> Readout(string[] readOut)
        {
            if (PreventReadoutAfterInjectionFlag)
            {
                throw new ArgumentException($"All injections must come before Readout");
            }
            __Readout = readOut;

            return this;   
        }

        /// <summary>
        /// replaces any instance of `Header` with Id in the CommandText and Readout
        /// 
        /// adds the Parameter to the parameter list in CommandText 
        /// </summary>
        /// 
        /// <param name="Header"></param>
        /// <param name="Parameter"></param>
        /// <param name="Value"></param>
        /// 
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="TypeLoadException"></exception>
        public Action<T> ParseId (string Header, string Parameter, object? Value)
        {
            if (PreventInjectionBeforeCommandFlag)
            {
                throw new ArgumentException($"Command Text not set... use Command");
            }
            if (Header == Parameter)
            {
                throw new ArgumentException($"{nameof(Header)} == {nameof(Parameter)}");
            }
            if (Header == "#Table" || Parameter == "#Table")
            {
                throw new ArgumentException($"#Table is reserved");
            }

            // this prevents the Readout from being called
            // since it would not work after this has passed
            PreventReadoutAfterInjectionFlag = true;

            /* Fill the Header */

            // replace occurances in the Command Text
            __SqliteCommand.CommandText = __SqliteCommand
                .CommandText
                    .Replace(Header, Id);

            // replace occurances in the Readout list
            if (__Readout is not null)
            {
                __Readout.Replace(Header, Id);
            }

            /* Fill the Parameter */

            if (Value is not int) {
                throw new TypeLoadException($"{Value}");
            }

            SqliteParameter __SqliteParameter = __SqliteCommand.CreateParameter();
            __SqliteParameter.ParameterName = Parameter;
            __SqliteParameter.Value = Value;
            __SqliteCommand.Parameters.Add(__SqliteParameter);

            return this;
        }

        /// <summary>
        /// replaces any instance of `Header` with the template in the CommandText and Readout
        /// 
        /// adds the Parameter to the parameter list in CommandText 
        /// </summary>
        /// <typeparam name="T2"></typeparam>
        /// <param name="Header"></param>
        /// <param name="Parameter"></param>
        /// <param name="Value"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="TypeLoadException"></exception>
        /// <exception cref="KeyNotFoundException"></exception>
        public Action<T> Parse<T2> (string Header, string Parameter, object? Value)
        where T2: IDbSqliteContextColumn
        {
            if (PreventInjectionBeforeCommandFlag)
            {
                throw new ArgumentException($"Command Text not set... use Command");
            }
            if (Header == Parameter)
            {
                throw new ArgumentException($"{nameof(Header)} == {nameof(Parameter)}");
            }
            if (Header == "#Table" || Parameter == "#Table")
            {
                throw new ArgumentException($"#Table is reserved");
            }

            // this prevents the Readout from being called
            // since it would not work after this has passed
            PreventReadoutAfterInjectionFlag = true;

            // Column
            string Column = typeof(T2).Name;
            MirrorToDatabase(__Table, Column);

            /* Fill the Header */

            // replace occurances in the Command Text
            __SqliteCommand.CommandText = __SqliteCommand
                .CommandText
                    .Replace(Header, Column);

            // replace occurances in the Readout list
            if (__Readout is not null)
            {
                __Readout.Replace(Header, Column);
            }

            /* Fill the Parameter */

            SqliteType? sqliteType = SqliteTypeOf(__Table, Column);
            if (sqliteType is null) throw new ArgumentNullException("sqliteType");

            switch (sqliteType)
            {
                case SqliteType.Integer: {
                    if (Value is not int && Value is not long) {
                        throw new TypeLoadException($"{sqliteType}");
                    }
                    break;
                }
                case SqliteType.Real: {
                    if (Value is not double && Value is not float) {
                        throw new TypeLoadException($"{sqliteType}");
                    }
                    break;
                }
                case SqliteType.Text: {
                    if (Value is not string && Value is not char) {
                        throw new TypeLoadException($"{sqliteType}");
                    }
                    break;
                }
                case SqliteType.Blob: {
                    if (Value is not byte[]) {
                        throw new TypeLoadException($"{sqliteType}");
                    }
                    break;
                }
                default:
                    throw new KeyNotFoundException($"{sqliteType}");
            }

            SqliteParameter __SqliteParameter = __SqliteCommand.CreateParameter();
            __SqliteParameter.ParameterName = Parameter;
            __SqliteParameter.Value = Value;
            __SqliteCommand.Parameters.Add(__SqliteParameter);

            return this;
        }

        /// <summary>
        /// replaces any instance of `Alias` with the template in the CommandText and Readout
        /// </summary>
        /// 
        /// <typeparam name="T2"></typeparam>
        /// <param name="Alias"></param>
        /// <returns></returns>
        public Action<T> Format<T2>(string Alias)
        where T2: IDbSqliteContextColumn
        {
            if (PreventInjectionBeforeCommandFlag)
            {
                throw new ArgumentException($"Command Text not set... use Command");
            }

            // this prevents the Readout from being called
            // since it would not work after this has passed
            PreventReadoutAfterInjectionFlag = true;

            string Column = typeof(T2).Name;

            __SqliteCommand.CommandText = __SqliteCommand
                .CommandText
                .Replace(Alias, Column);

            if (__Readout is not null)
            {
                __Readout.Replace(Alias, Column);
            }

            return this;
        }

        /// <summary>
        /// adds the Parameter to the parameter list in CommandText 
        /// </summary>
        /// 
        /// <typeparam name="T2"></typeparam>
        /// <param name="Alias"></param>
        /// <param name="Value"></param>
        /// 
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="TypeLoadException"></exception>
        /// <exception cref="KeyNotFoundException"></exception>
        public Action<T> Parameter<T2>(string Alias, object? Value)
        where T2: IDbSqliteContextColumn
        {
            if (PreventInjectionBeforeCommandFlag)
            {
                throw new ArgumentException($"Command Text not set... use Command");
            }

            // this prevents the Readout from being called
            // since it would not work after this has passed
            PreventReadoutAfterInjectionFlag = true;

            string Column = typeof(T2).Name;

            // ensure the database has Table/Column
            MirrorToDatabase(__Table, Column);
            SqliteType? sqliteType = SqliteTypeOf(__Table, Column);
            if (sqliteType is null) throw new ArgumentNullException("sqliteType");

            switch (sqliteType)
            {
                case SqliteType.Integer: {
                    if (Value is not int) {
                        throw new TypeLoadException($"{sqliteType}");
                    }
                    break;
                }
                case SqliteType.Real: {
                    if (Value is not double) {
                        throw new TypeLoadException($"{sqliteType}");
                    }
                    break;
                }
                case SqliteType.Text: {
                    if (Value is not string) {
                        throw new TypeLoadException($"{sqliteType}");
                    }
                    break;
                }
                case SqliteType.Blob: {
                    if (Value is not byte[]) {
                        throw new TypeLoadException($"{sqliteType}");
                    }
                    break;
                }
                default:
                    throw new KeyNotFoundException($"{sqliteType}");
            }
            
            SqliteParameter __SqliteParameter = __SqliteCommand.CreateParameter();
            __SqliteParameter.ParameterName = Alias;
            __SqliteParameter.Value = Value;
            __SqliteCommand.Parameters.Add(__SqliteParameter);

            return this;
        }

        /// <summary>
        /// calls Execute on the Command Text
        /// 
        /// if Readout is assigned, it uses a Reader mapped to the Readout
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="KeyNotFoundException"></exception>
        public List<object[]>? Execute()
        {
            if (__Readout is null) {
                if (__SqliteCommand.CommandText.Contains("SELECT"))
                {
                    Console.WriteLine("SELECT but no Readout");
                }

                __SqliteCommand.ExecuteNonQuery();
                return null;
            }

            List<object[]> result = new();
            using var __SqliteReader = __SqliteCommand.ExecuteReader();
            while (__SqliteReader.Read())
            {
                var tmp = new object[__Readout.Length];
                for (int i = 0; i < tmp.Length; i++)
                {
                    SqliteType? sqliteType = SqliteTypeOf(__Table, __Readout[i]);
                    if (sqliteType is null) throw new ArgumentNullException($"{__Readout[i]}");

                    switch(sqliteType)
                    {
                        case SqliteType.Integer: {
                            tmp[i] = __SqliteReader.GetInt64(i);
                            break;
                        }
                        case SqliteType.Real: {
                            tmp[i] = __SqliteReader.GetDouble(i);
                            break;
                        }
                        case SqliteType.Text: {
                            tmp[i] = __SqliteReader.GetString(i);
                            break;
                        }
                        case SqliteType.Blob: {
                            tmp[i] = __SqliteReader.GetValue(i);
                            break;
                        }
                        default: 
                            throw new KeyNotFoundException($"{sqliteType}");
                    }
                }
                result.Add(tmp);
            }
            return result;
        }
    }
}