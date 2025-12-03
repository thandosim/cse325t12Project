using DotNetEnv;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using t12Project.Components;
using t12Project.Data;
using t12Project.Models;
using t12Project.Options;
using t12Project.Services;

Env.Load();

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}

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

// Configure Identity cookie paths to use our custom Blazor routes
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
    options.AccessDeniedPath = "/access-denied";
});

var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
builder.Services.Configure<JwtOptions>(jwtSection);
var jwtOptions = jwtSection.Get<JwtOptions>();
if (jwtOptions is null || string.IsNullOrWhiteSpace(jwtOptions.SigningKey))
{
    if (builder.Environment.IsDevelopment())
    {
        // Provide a safe development fallback so local runs don't require production secrets.
        jwtOptions = new JwtOptions { SigningKey = "dev-signing-key-please-change", Issuer = "dev", Audience = "dev" };
    }
    else
    {
        throw new InvalidOperationException("JWT configuration missing. Ensure Jwt:SigningKey, Issuer, Audience are set.");
    }
}

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));

// Configure authentication. Register JWT always; register Google only when credentials are present.
var authBuilder = builder.Services.AddAuthentication();
authBuilder.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = true;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        ValidIssuer = jwtOptions.Issuer,
        ValidAudience = jwtOptions.Audience,
        IssuerSigningKey = signingKey,
        ClockSkew = TimeSpan.FromMinutes(1)
    };
});

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    authBuilder.AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
    });
}
else
{
    // No Google credentials found. In Development this is expected for local runs.
    // The application will continue without Google external login configured.
}

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("DriverOnly", policy => policy.RequireRole("Driver"));
    options.AddPolicy("CustomerOnly", policy => policy.RequireRole("Customer"));
});

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
// Additional services from main branch: authentication helpers, controllers and HTTP utilities
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddScoped<ProtectedLocalStorage>();

// Application services
builder.Services.AddScoped<DatabaseService>();
builder.Services.AddScoped<AdminUserService>();
builder.Services.AddScoped<IAuthTokenService, AuthTokenService>();
builder.Services.AddScoped<AuthClient>();

var app = builder.Build();

// Run database seed/migrations. In Development, don't crash the app if the DB isn't available;
// instead log a warning so developers without Postgres can still run the app locally.
var logger = app.Services.GetRequiredService<ILogger<Program>>();
if (!app.Environment.IsDevelopment())
{
    try
    {
        await SeedData.InitializeAsync(app.Services);
    }
    catch (Exception ex)
    {
        // In non-development environments, fail fast so issues are addressed.
        logger.LogCritical(ex, "Database seed/migration failed during startup.");
        throw;
    }
}
else
{
    // In Development, only run the seed if explicitly enabled via RUN_DB_SEED=true
    var runSeed = (Environment.GetEnvironmentVariable("RUN_DB_SEED") ?? "").ToLowerInvariant() == "true";
    if (runSeed)
    {
        try
        {
            logger.LogInformation("RUN_DB_SEED=true — running database seed/migration in Development.");
            await SeedData.InitializeAsync(app.Services);
        }
        catch (Exception ex)
        {
            // Log and continue — developers may not have a working DB locally.
            logger.LogWarning(ex, "Database seed/migration failed in Development while RUN_DB_SEED=true; continuing without DB initialization.");
        }
    }
    else
    {
        logger.LogInformation("Development environment detected — skipping database seed/migration. Set RUN_DB_SEED=true to enable.");
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
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
