using DotNetEnv;
using t12Project.Components;
using t12Project.Services;

Env.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient();

builder.Services.AddScoped<DatabaseService>();

// Register mock account service for development
builder.Services.AddSingleton<IAccountService, MockAccountService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

// Minimal API endpoints for account (dev/mock persistence)
app.MapGet("/api/account", async (IAccountService svc) =>
{
    var acc = await svc.GetAsync();
    return acc is null ? Results.NoContent() : Results.Json(acc);
});

app.MapPut("/api/account", async (IAccountService svc, AccountDto dto) =>
{
    await svc.SaveAsync(dto);
    return Results.Ok();
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
