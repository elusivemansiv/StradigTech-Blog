using System;

public class Program
{
    public static void Main()
    {
        TestParsing("mysql://user:pass@host:3306/db");
        TestParsing("mysql://user:pass@host/db");
    }

    public static void TestParsing(string rawConnection)
    {
        Console.WriteLine($"Testing: {rawConnection}");
        try
        {
            var uri = new Uri(rawConnection);
            var userInfo = uri.UserInfo.Split(':');
            var dbName = uri.AbsolutePath.TrimStart('/');
            var port = uri.Port > 0 ? uri.Port : 3306;

            var connectionString = $"Server={uri.Host};Port={port};Database={dbName};User={userInfo[0]};Password={userInfo[1]};AllowPublicKeyRetrieval=true;SslMode=Preferred;";
            Console.WriteLine($"Result: Server={uri.Host};Port={port};Database={dbName};User={userInfo[0]};Password=***");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        Console.WriteLine();
    }
}
