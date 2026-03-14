using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StradigBlog.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllersWithViews();

// -----------------------------
// Database connection setup
// -----------------------------
Console.WriteLine("[DEBUG] Checking Environment Variables for Database...");

string? rawUrl = Environment.GetEnvironmentVariable("DATABASE_URL") 
               ?? Environment.GetEnvironmentVariable("MYSQL_URL");

string connectionString;

if (!string.IsNullOrWhiteSpace(rawUrl) && (rawUrl.StartsWith("mysql://") || rawUrl.StartsWith("mariadb://")))
{
    try
    {
        var uri = new Uri(rawUrl);
        var userInfo = uri.UserInfo.Split(':');
        var user = userInfo.Length > 0 ? userInfo[0] : "";
        var password = userInfo.Length > 1 ? userInfo[1] : "";
        var dbName = uri.AbsolutePath.TrimStart('/');
        var port = uri.Port > 0 ? uri.Port : 3306;

        connectionString = $"Server={uri.Host};Port={port};Database={dbName};User={user};Password={password};AllowPublicKeyRetrieval=true;SslMode=Preferred;";
        Console.WriteLine($"[INFO] Parsed connection from URL. Target: {uri.Host}:{port}/{dbName}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] Failed to parse connection URL: {ex.Message}");
        connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "";
    }
}
else
{
    // Try individual Railway variables if URL is missing
    var host = Environment.GetEnvironmentVariable("MYSQLHOST");
    var user = Environment.GetEnvironmentVariable("MYSQLUSER");
    var pass = Environment.GetEnvironmentVariable("MYSQLPASSWORD");
    var db = Environment.GetEnvironmentVariable("MYSQLDATABASE");
    var port = Environment.GetEnvironmentVariable("MYSQLPORT") ?? "3306";

    if (!string.IsNullOrEmpty(host) && !string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(db))
    {
        connectionString = $"Server={host};Port={port};Database={db};User={user};Password={pass};AllowPublicKeyRetrieval=true;SslMode=Preferred;";
        Console.WriteLine($"[INFO] Formed connection from individual MYSQL variables. Target: {host}:{port}/{db}");
    }
    else
    {
        connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "";
        Console.WriteLine(string.IsNullOrEmpty(connectionString) 
            ? "[WARNING] No database connection found!" 
            : "[INFO] Using fallback connection string from config.");
    }
}

// Register DbContext with MySQL and retry on transient failures
builder.Services.AddDbContext<BlogDbContext>(options =>
    options.UseMySql(
        connectionString,
        new MySqlServerVersion(new Version(8, 0, 32)),
        mySqlOptions => mySqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null
        )
    )
);



// -----------------------------
// Port Configuration for Railway
// -----------------------------
var portEnv = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(portEnv))
{
    builder.WebHost.UseUrls($"http://*:{portEnv}");
    Console.WriteLine($"[INFO] App configured to listen on PORT: {portEnv}");
}


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

// -----------------------------
// Middleware pipeline
// -----------------------------
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
// Auto-migrate database on startup
// -----------------------------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BlogDbContext>();
    try
    {
        db.Database.Migrate();
        Console.WriteLine("[INFO] Database migrated successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] Failed to migrate database: {ex.Message}");
        throw;
    }
}

app.Run();