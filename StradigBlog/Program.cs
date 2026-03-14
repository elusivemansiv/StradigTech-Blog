using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StradigBlog.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllersWithViews();

// -----------------------------
// Database connection setup
// -----------------------------
var rawConnection = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "";

string connectionString;

if (!string.IsNullOrWhiteSpace(rawConnection) && rawConnection.StartsWith("mysql://"))
{
    try
    {
        var uri = new Uri(rawConnection);
        var userInfo = uri.UserInfo.Split(':');
        var dbName = uri.AbsolutePath.TrimStart('/');
        var port = uri.Port > 0 ? uri.Port : 3306;

        connectionString = $"Server={uri.Host};Port={port};Database={dbName};User={userInfo[0]};Password={userInfo[1]};AllowPublicKeyRetrieval=true;SslMode=Preferred;";
        Console.WriteLine($"[INFO] Connecting to MySQL at {uri.Host}:{port}/{dbName}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR] Failed to parse DATABASE_URL: {ex.Message}");
        connectionString = rawConnection;
    }
}
else
{
    connectionString = rawConnection;
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