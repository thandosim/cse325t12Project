# Developer instructions — Postgres integration (Account)

This file explains how to replace the in-memory `MockAccountService` with a Postgres-backed implementation using EF Core + Npgsql, and how to register it in `Program.cs`.

1) Add packages

Use NuGet packages (project-level) and the EF Core tools for migrations.

PowerShell:

```powershell
dotnet add package Microsoft.EntityFrameworkCore
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet tool install --global dotnet-ef
```

2) Create the EF Core pieces

- Create an entity model `Models/Account.cs` with properties (Id, FirstName, LastName, Email, Phone, OwnerId, etc.).
- Create a `Data/AppDbContext.cs` deriving from `DbContext` and add a `DbSet<Account> Accounts`.

Example `AppDbContext` snippet:

```csharp
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> opts) : base(opts) { }
    public DbSet<Account> Accounts { get; set; }
}
```

3) Implement `PostgresAccountService` (implements `IAccountService`)

- Use `AppDbContext` in the service, implement `GetAsync()` and `SaveAsync()` logic to read/update the account row for the current user (or a single row if your app is single-account).

Minimal example:

```csharp
public class PostgresAccountService : IAccountService
{
    private readonly AppDbContext _db;
    public PostgresAccountService(AppDbContext db) => _db = db;

    public async Task<AccountDto?> GetAsync()
    {
        var entity = await _db.Accounts.FirstOrDefaultAsync();
        return entity is null ? null : new AccountDto { FirstName = entity.FirstName, LastName = entity.LastName, Email = entity.Email, Phone = entity.Phone };
    }

    public async Task SaveAsync(AccountDto dto)
    {
        var entity = await _db.Accounts.FirstOrDefaultAsync();
        if (entity == null) { entity = new Account(); _db.Accounts.Add(entity); }
        entity.FirstName = dto.FirstName; entity.LastName = dto.LastName; entity.Email = dto.Email; entity.Phone = dto.Phone;
        await _db.SaveChangesAsync();
    }
}
```

4) Register services in `Program.cs`

Replace the mock registration with EF Core and the Postgres service. Example (inside `var builder = WebApplication.CreateBuilder(args);`):

```csharp
var conn = Environment.GetEnvironmentVariable("AZURE_POSTGRES_CONNECTION") ?? builder.Configuration["AZURE_POSTGRES_CONNECTION"];
builder.Services.AddDbContext<AppDbContext>(opts => opts.UseNpgsql(conn));
builder.Services.AddScoped<IAccountService, PostgresAccountService>();
```

5) Migrations and database update

Run migrations from the project directory:

```powershell
dotnet ef migrations add AddAccountEntity
dotnet ef database update
```

6) Connection string and environment

- The repo uses the env var `AZURE_POSTGRES_CONNECTION` (in `.env`). Example connection string (replace placeholders):

```
Host=your-db-host;Port=5432;Database=yourdb;Username=youruser;Password=yourpassword;Pooling=true;Minimum Pool Size=1;Maximum Pool Size=20
```

7) DatabaseService startup check

Note: `Services/DatabaseService` currently runs a test connection at startup and may cause the host to shut down if authentication fails. Options for the Postgres owner:

- Ensure `AZURE_POSTGRES_CONNECTION` is valid in `appsettings`/env for the environment where the app runs.
- Modify `DatabaseService.TestConnectionAsync` to only log connection failures in `Development` instead of terminating the application.

8) Migrating existing mock data (optional)

If developers used the `MockAccountService` and want to migrate its in-memory account to Postgres, you can add a small one-off migration step in `Program.cs` (Development-only) to read the mock instance and save to the DB.

9) Notes & tips

- Consider adding `IAccountService` methods that accept an `ownerId` or use the authenticated user identity so production can support multiple users.
- Add `Microsoft.EntityFrameworkCore.Tools` as a development dependency if needed.
- Test locally by setting `ASPNETCORE_ENVIRONMENT=Development` and `AZURE_POSTGRES_CONNECTION` to a local Postgres instance first.

If you want, I can implement `PostgresAccountService` and `AppDbContext` now and wire migrations (you will need to provide or test with a Postgres connection string). Otherwise this document should be enough for the Postgres owner to proceed.
