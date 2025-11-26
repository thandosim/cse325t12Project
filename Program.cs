using DotNetEnv;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using t12Project.Components;
using t12Project.Data;
using t12Project.Models;
using t12Project.Services;

Env.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

// Connection strings
var postgresConnection = builder.Configuration["AZURE_POSTGRES_CONNECTION"]
    ?? builder.Configuration.GetConnectionString("DefaultConnection");

// Flag to toggle database provider
var usePostgres = builder.Configuration.GetValue<bool>("UsePostgres"); // set in appsettings or env
var useSqliteDev = builder.Environment.IsDevelopment() && !usePostgres; // default to SQLite in dev

// -------------------- Database Setup --------------------
if (usePostgres)
{
    AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(postgresConnection));

    builder.Services.AddDatabaseDeveloperPageExceptionFilter();
}
else if (useSqliteDev)
{
    // SQLite file for dev mode
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite("Data Source=app_dev.db"));
}
else
{
    // Fallback mock service if neither DB is configured
    builder.Services.AddScoped<MockDatabaseService>();
}

// -------------------- Identity + Auth --------------------
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequiredLength = 8;
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// Authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("DriverOnly", policy => policy.RequireRole("Driver"));
    options.AddPolicy("CustomerOnly", policy => policy.RequireRole("Customer"));
});

// -------------------- Services --------------------
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<MapDataService>();
builder.Services.AddScoped<DatabaseService>();
builder.Services.AddScoped<AdminUserService>();

builder.Services.AddCascadingAuthenticationState();

builder.Services.AddServerSideBlazor()
    .AddCircuitOptions(options => { options.DetailedErrors = true; });

// -------------------- Build App --------------------
var app = builder.Build();

// Seed data only if a real DB is configured
if (usePostgres || useSqliteDev)
{
    try
    {
        await SeedData.InitializeAsync(app.Services);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Database seeding skipped: {ex.Message}");
    }
}

// -------------------- Middleware --------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

// Map Identity Razor Pages (login/register/logout)
app.MapRazorPages();

// Map Blazor components
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
