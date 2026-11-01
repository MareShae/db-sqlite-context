using System.Text.Json.Nodes;

namespace DbSqliteContext;


/**
 * an easy way to construct databases and properly reference them for type-checking
 * I am looking for container (CONTAINER) that store info {INFO=Info} as another of the same container
 * where the CONTAINER can be implicitly cast to string
 * yet, as a container it has . access, like CONTAINER.INFO
**/




class Program
{
    static void Main()
    {
        // delete the previous .db
        if (File.Exists(Example.__Source))
        {
            Console.WriteLine($"delete {Example.__Source} for example");
            File.Delete(Example.__Source);
        }

        // shows the structure of the object
        Console.WriteLine(Example.__JsonStructure);

        // breaks down the structure to only show the tables and their columns
        var Tables = Example.GetTableInStructure();
        if (Tables is not null) {
            foreach(var table in Tables)
            {
                Console.WriteLine($"{table}, ");
                var Columns = Example.GetColumnInStructure(table);
                if (Columns is not null)
                {
                    foreach(var column in Columns)
                    {
                        Console.WriteLine($"\t{column}, ");
                    }
                }
                else
                {
                    Console.WriteLine("No Columns");
                }
            }
        } else
        {
            Console.WriteLine("No Tables");
        }

        var random = new Random();

        /* test create */
        Console.WriteLine("------ KEY TEST CREATE ------");
        Console.WriteLine("create items for Table1");
        for (int i = 0; i < 6; i++)
        {
            /* smol data for database */
            
            new Example.Action<Example.Table1>()
                .Command(
                    $"""
                    INSERT INTO #Table (#1, #2)
                    VALUES (@1, @2)
                    """
                )
                .Parse(Example.Table1.Text, "#1", "@1", random.GetHexString(10))
                .Parse(Example.Table1.Number, "#2", "@2", random.Next(100))
                .Execute();
        }
        Console.WriteLine("create items for Table2 with reference(foreign) keys");
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 2; j++)
            {
                new Example.Action<Example.Table2>()
                .Command(
                    $"""
                    INSERT INTO #Table (#1, #2)
                    VALUES (@1, @2)
                    """
                )
                .Parse(Example.Table2.Reference, "#1", "@1", i + 1)
                .Parse(Example.Table2.Randstr, "#2", "@2", random.GetHexString(10))
                .Execute();
            }
        }
        Example.ShowAllTableInDatabase();
        Console.WriteLine("-----------------------------");


        /* test update */
        Console.WriteLine("------ KEY TEST UPDATE ------");
        Console.WriteLine("update odd for Table1");
        for (int i = 1; i <= 6; i+=2) {
            new Example.Action<Example.Table1>()
                .Command(
                    $"""
                    UPDATE #Table
                    SET #1=@1, #2=@2
                    WHERE #3=@3
                    """
                )
                .Parse(Example.Table1.Text, "#1", "@1", $"Random{random.GetHexString(4)}")
                .Parse(Example.Table1.Number, "#2", "@2", -random.Next(10000000))
                .ParseId("#3", "@3", i)
                .Execute();
        }
        Example.ShowAllTableInDatabase();
        Console.WriteLine("-----------------------------");


        /* test delete */
        Console.WriteLine("------ KEY TEST DELETE ------");
        Console.WriteLine("delete even for Table1");
        for (int i = 2; i <= 6; i+=2) {
            new Example.Action<Example.Table1>()
                .Command(
                    $"""
                    DELETE
                    FROM #Table
                    WHERE #1=@1
                    """
                )
                .ParseId("#1", "@1", i)
                .Execute();
        }
        Example.ShowAllTableInDatabase();
        Console.WriteLine("-----------------------------");


        /* test select */
        Console.WriteLine("------ KEY TEST SELECT ------");
        var select = new Example.Action<Example.Table1>()
            .Command(
                $"""
                SELECT #1, #2
                FROM #Table
                """
            )
            .Readout(["#1", "#2"])
            .Format(Example.Table1.Text, "#1")
            .Format(Example.Table1.Number, "#2")
            .Execute();
        if (select is not null)
        {
            Console.WriteLine($"Select has {select.Count} items");
            foreach (object[] item in select)
            {
                Console.WriteLine(
                    string.Join(" | ", item.Select(i => i.ToString()))
                );
            }
        } else
        {
            Console.WriteLine("select is null... something went wrong");
        }

        Console.WriteLine("-----------------------------");
    }

}

