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
        // // shows the structure of the object
        // Console.WriteLine(KeyTest.__JsonStructure);

        // // breaks down the structure to only show the tables and their columns
        // var Tables = KeyTest.GetTableInStructure();
        // if (Tables is not null) {
        //     Console.WriteLine("<Tables>");
        //     foreach(var table in Tables)
        //     {
        //         Console.WriteLine($"{table}, ");
        //         var Columns = KeyTest.GetColumnInStructure(table);
        //         if (Columns is not null)
        //         {
        //             Console.WriteLine("\t<Columns>");
        //             foreach(var column in Columns)
        //             {
        //                 Console.WriteLine($"\t{column}, ");
        //             }
        //             Console.WriteLine("\t</Columns>");
        //         }
        //         else
        //         {
        //             Console.WriteLine("No Columns");
        //         }
        //     }
        //     Console.WriteLine("</Tables>");
        // } else
        // {
        //     Console.WriteLine("No Tables");
        // }


        // /* test create */
        // Console.WriteLine("------ KEY TEST CREATE ------");
        // var random = new Random();
        // for (int i = 0; i < 6; i++)
        // {
        //     /* smol data for database */
            
        //     new KeyTest.Action<KeyTest.Table1>()
        //         .Command(
        //             $"""
        //             INSERT INTO #Table (#1, #2)
        //             VALUES (@1, @2)
        //             """
        //         )
        //         .Parse<KeyTest.Table1.Text>("#1", "@1", random.GetHexString(10))
        //         .Parse<KeyTest.Table1.Number>("#2", "@2", random.Next(100))
        //         .Execute();
        // }
        // KeyTest.ShowAllTableInDatabase();
        // Console.WriteLine("-----------------------------");


        /* test update */
        Console.WriteLine("------ KEY TEST UPDATE ------");
        new Example.Action<Example.Table1>()
            .Command(
                $"""
                UPDATE #Table
                SET #1=@1, #2=@2
                WHERE #3=@3
                """
            )
            .Parse<Example.Table1.Text>("#1", "@1", "NotARandomString")
            .Parse<Example.Table1.Number>("#2", "@2", -2428896545)
            .ParseId("#3", "@3", 3)
            .Execute();
        Example.ShowAllTableInDatabase();
        Console.WriteLine("-----------------------------");


        /* test delete */
        Console.WriteLine("------ KEY TEST DELETE ------");
        new Example.Action<Example.Table1>()
            .Command(
                $"""
                DELETE
                FROM #Table
                WHERE #1=@1
                """
            )
            .ParseId("#1", "@1", 2)
            .Execute();
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
            .Format<Example.Table1.Text>("#1")
            .Format<Example.Table1.Number>("#2")
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

