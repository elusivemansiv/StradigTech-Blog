using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StradigBlog.Data;
using MySqlConnector;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllersWithViews();

// -----------------------------
// Database connection setup
// -----------------------------
Console.WriteLine("[DEBUG] --- Environment Diagnostics ---");
var allEnvVars = Environment.GetEnvironmentVariables();
Console.WriteLine("[DEBUG] Relevant Environment Variables found:");
foreach (var key in allEnvVars.Keys) {
    string keyStr = key?.ToString() ?? "";
    if (keyStr.Contains("PORT", StringComparison.OrdinalIgnoreCase) || 
        keyStr.Contains("MYSQL", StringComparison.OrdinalIgnoreCase) || 
        keyStr.Contains("DATABASE", StringComparison.OrdinalIgnoreCase) || 
        keyStr.Contains("DB", StringComparison.OrdinalIgnoreCase) ||
        keyStr.Contains("URL", StringComparison.OrdinalIgnoreCase))
    {
        var val = allEnvVars[key]?.ToString() ?? "";
        string displayVal = (keyStr.Contains("PASS", StringComparison.OrdinalIgnoreCase) || keyStr.Contains("URL", StringComparison.OrdinalIgnoreCase)) 
            ? "***masked***" 
            : val;
        Console.WriteLine($"  -> {keyStr} = {displayVal}");
    }
}

// 1. Try URL-based connection strings (Priority: Private -> Public -> Default)
string? rawUrl = Environment.GetEnvironmentVariable("MYSQL_PRIVATE_URL") 
               ?? Environment.GetEnvironmentVariable("MYSQL_URL")
               ?? Environment.GetEnvironmentVariable("DATABASE_URL");

var builderConn = new MySqlConnectionStringBuilder();

if (!string.IsNullOrWhiteSpace(rawUrl) && (rawUrl.StartsWith("mysql://") || rawUrl.StartsWith("mariadb://")))
{
    try
    {
        var uri = new Uri(rawUrl);
        var userInfo = uri.UserInfo.Split(':');
        
        builderConn.Server = uri.Host;
        builderConn.Port = (uint)(uri.Port > 0 ? uri.Port : 3306);
        builderConn.UserID = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "";
        builderConn.Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
        builderConn.Database = uri.AbsolutePath.TrimStart('/').Split('?')[0].TrimEnd('/');
        
        // Common Railway settings
        builderConn.AllowPublicKeyRetrieval = true;
        builderConn.SslMode = uri.Host.EndsWith(".internal") ? MySqlSslMode.None : MySqlSslMode.Preferred;

        Console.WriteLine($"[INFO] Parsed connection from URL. Target: {builderConn.Server}:{builderConn.Port}/{builderConn.Database} (SSL: {builderConn.SslMode})");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] Failed to parse connection URL: {ex.Message}");
    }
}

string connectionString = builderConn.ConnectionString;

// 2. Fallback to individual variables if URL parsing failed or was empty
if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(builderConn.Server))
{
    var host = Environment.GetEnvironmentVariable("MYSQLHOST");
    var user = Environment.GetEnvironmentVariable("MYSQLUSER");
    var pass = Environment.GetEnvironmentVariable("MYSQLPASSWORD");
    var db = Environment.GetEnvironmentVariable("MYSQLDATABASE");
    var port = Environment.GetEnvironmentVariable("MYSQLPORT") ?? "3306";

    if (!string.IsNullOrEmpty(host) && !string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(db))
    {
        var fallbackBuilder = new MySqlConnectionStringBuilder
        {
            Server = host,
            UserID = user,
            Password = pass,
            Database = db,
            Port = uint.Parse(port),
            AllowPublicKeyRetrieval = true,
            SslMode = MySqlSslMode.Preferred
        };
        connectionString = fallbackBuilder.ConnectionString;
        Console.WriteLine($"[INFO] Formed connection from individual MYSQL variables. Target: {host}:{port}/{db}");
    }
}

// 3. Last fallback: appsettings.json
if (string.IsNullOrEmpty(connectionString))
{
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "";
    Console.WriteLine(string.IsNullOrEmpty(connectionString) 
        ? "[WARNING] No database connection found!" 
        : "[INFO] Using fallback connection string from config.");
}

// Register DbContext with MySQL and retry on transient failures
builder.Services.AddDbContext<BlogDbContext>(options =>
    options.UseMySql(
        connectionString,
        new MySqlServerVersion(new Version(8, 0, 32)),
        mySqlOptions => mySqlOptions.EnableRetryOnFailure(
            maxRetryCount: 10,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null
        )
    )
);




// -----------------------------
// Port Configuration for Railway
// -----------------------------
var portEnv = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{portEnv}");
Console.WriteLine($"[INFO] App configured to listen on PORT: {portEnv} (0.0.0.0)");


// -----------------------------
// Identity configuration
// -----------------------------
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;

    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;

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

app.UseDeveloperExceptionPage(); // Keep enabled temporarily to debug 500 error on Railway

// -----------------------------
// Middleware pipeline
// -----------------------------
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

// -----------------------------
// Auto-migrate database on startup (Non-blocking)
// -----------------------------
_ = Task.Run(async () => 
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
        try
        {
            Console.WriteLine("[INFO] Background migration starting in 2 seconds...");
            await Task.Delay(2000); 
            Console.WriteLine("[INFO] Running database migrations...");
            db.Database.Migrate();
            Console.WriteLine("[INFO] Database migrated successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to migrate database: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"[DEBUG] Inner Exception: {ex.InnerException.Message}");
            }
        }
    }
});

app.Run();