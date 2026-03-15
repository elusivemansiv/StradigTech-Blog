using Humanizer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using StradigBlog.Data;
using StradigBlog.Models;

var builder = WebApplication.CreateBuilder(args);

// Railway PORT configuration
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
Console.WriteLine($"Starting application on port: {port}");
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Add services
builder.Services.AddControllersWithViews();

// Register DbContext
builder.Services.AddDbContext<BlogDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    
    // Fallback to individual Railway variables if DefaultConnection is not a valid MySQL string
    var rHost = Environment.GetEnvironmentVariable("MYSQLHOST");
    var rUser = Environment.GetEnvironmentVariable("MYSQLUSER");
    var rPass = Environment.GetEnvironmentVariable("MYSQLPASSWORD");
    var rPort = Environment.GetEnvironmentVariable("MYSQLPORT") ?? "3306";
    var rDb = Environment.GetEnvironmentVariable("MYSQLDATABASE") ?? "railway";

    if (!string.IsNullOrEmpty(rHost) && (string.IsNullOrEmpty(connectionString) || connectionString.Contains("localdb")))
    {
        Console.WriteLine("Detected Railway MySQL environment variables. Overriding connection string.");
        connectionString = $"Server={rHost};Port={rPort};Database={rDb};Uid={rUser};Pwd={rPass};SslMode=None;";
    }
    else if (connectionString != null && connectionString.StartsWith("mysql://"))
    {
        try
        {
            var uri = new Uri(connectionString);
            var userInfo = uri.UserInfo.Split(':');
            var user = Uri.UnescapeDataString(userInfo[0]);
            var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
            var host = uri.Host;
            var portStr = uri.Port > 0 ? uri.Port : 3306;
            var database = uri.AbsolutePath.TrimStart('/');
            
            connectionString = $"Server={host};Port={portStr};Database={database};Uid={user};Pwd={password};SslMode=None;";
            Console.WriteLine($"Parsed MySQL Connection String: Server={host};Database={database};Port={portStr}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing MySQL URL: {ex.Message}");
        }
    }

    Console.WriteLine($"Final Connection String: {connectionString?.Split('@').LastOrDefault() ?? "NULL"} (redacted)");

    // Using a fixed version to avoid AutoDetect hanging if DB is not ready
    var serverVersion = new MySqlServerVersion(new Version(8, 0, 0));
    options.UseMySql(connectionString, serverVersion, mysqlOptions => 
    {
        mysqlOptions.EnableRetryOnFailure(
            maxRetryCount: 10,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null);
    });
});

// Add Identity
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;

    // User settings
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<BlogDbContext>()
.AddDefaultTokenProviders();

// Cookie settings
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/Login";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
});

builder.Services.AddAuthorization();

var app = builder.Build();

// Auto-apply migrations
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    Console.WriteLine("Checking database migrations...");
    try
    {
        var context = services.GetRequiredService<BlogDbContext>();
        context.Database.Migrate();
        Console.WriteLine("Database migrations applied successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Migration Error: {ex.Message}");
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database.");
    }
}

// if (!app.Environment.IsDevelopment())
// {
//     app.UseExceptionHandler("/Home/Error");
//     app.UseHsts();
// }

app.UseDeveloperExceptionPage(); 

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// IMPORTANT: Authentication must come before Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok("Healthy"));

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();