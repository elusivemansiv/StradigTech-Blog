using System;
using MySqlConnector;

public class Program
{
    public static void Main()
    {
        TestParsing("mysql://user:pass;with;semicolon@mysql.railway.internal:3306/railway");
        TestParsing("mysql://root:secure_pw@host.com/database?reconnect=true");
    }

    public static void TestParsing(string rawUrl)
    {
        Console.WriteLine($"Testing URL: {rawUrl}");
        try
        {
            var uri = new Uri(rawUrl);
            var userInfo = uri.UserInfo.Split(':');
            
            var builder = new MySqlConnectionStringBuilder();
            builder.Server = uri.Host;
            builder.Port = (uint)(uri.Port > 0 ? uri.Port : 3306);
            builder.UserID = Uri.UnescapeDataString(userInfo[0]);
            builder.Password = Uri.UnescapeDataString(userInfo[1]);
            builder.Database = uri.AbsolutePath.TrimStart('/').Split('?')[0].TrimEnd('/');
            builder.AllowPublicKeyRetrieval = true;
            builder.SslMode = uri.Host.EndsWith(".internal") ? MySqlSslMode.None : MySqlSslMode.Preferred;

            Console.WriteLine($"Resulting Connection String: {builder.ConnectionString}");
            Console.WriteLine($"Server: {builder.Server}");
            Console.WriteLine($"Port: {builder.Port}");
            Console.WriteLine($"UserID: {builder.UserID}");
            Console.WriteLine($"Password: {builder.Password}");
            Console.WriteLine($"Database: {builder.Database}");
            Console.WriteLine($"SslMode: {builder.SslMode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        Console.WriteLine("-----------------------------------");
    }
}
