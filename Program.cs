using DotNetEnv;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using t12Project.Components;
using t12Project.Data;
using t12Project.Models;
using t12Project.Services;

Env.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

var connectionString = builder.Configuration["AZURE_POSTGRES_CONNECTION"]
    ?? builder.Configuration.GetConnectionString("DefaultConnection");

// Temporarily allow running without database for UI development
var useDatabaseAuth = !string.IsNullOrWhiteSpace(connectionString);

if (useDatabaseAuth)
{
    AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(connectionString));

    builder.Services.AddDatabaseDeveloperPageExceptionFilter();

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

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
        options.AddPolicy("DriverOnly", policy => policy.RequireRole("Driver"));
        options.AddPolicy("CustomerOnly", policy => policy.RequireRole("Customer"));
    });
}

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();

builder.Services.AddScoped<DatabaseService>();
builder.Services.AddScoped<AdminUserService>();

var app = builder.Build();

// Only seed data if database is configured
if (useDatabaseAuth)
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

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
else
{
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

if (useDatabaseAuth)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
