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

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("A database connection string is required. Set ConnectionStrings:DefaultConnection or AZURE_POSTGRES_CONNECTION in .env.");
}

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

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<DatabaseService>();

var app = builder.Build();

// Run database seed/migrations. In Development, don't crash the app if the DB isn't available;
// instead log a warning so developers without Postgres can still run the app locally.
var logger = app.Services.GetRequiredService<ILogger<Program>>();
try
{
    await SeedData.InitializeAsync(app.Services);
}
catch (Exception ex)
{
    if (app.Environment.IsDevelopment())
    {
        logger.LogWarning(ex, "Database seed/migration failed — continuing in Development mode without DB initialization.");
    }
    else
    {
        // In non-development environments, fail fast so issues are addressed.
        logger.LogCritical(ex, "Database seed/migration failed during startup.");
        throw;
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

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
