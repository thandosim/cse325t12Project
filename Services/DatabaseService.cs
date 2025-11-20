using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace t12Project.Services;

public class DatabaseService
{
    private readonly string? _connectionString;
    private readonly ILogger<DatabaseService> _logger;

    public DatabaseService(IConfiguration configuration, ILogger<DatabaseService> logger)
    {
        _connectionString = configuration.GetConnectionString("AzurePostgres")
            ?? configuration["AZURE_POSTGRES_CONNECTION"];
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_connectionString);

    public async Task<DatabaseConnectionResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            const string message = "Connection string not configured. Set AZURE_POSTGRES_CONNECTION in .env.";
            _logger.LogWarning(message);
            return DatabaseConnectionResult.Failure(message);
        }

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var version = connection.PostgreSqlVersion;

            await using var command = new NpgsqlCommand("SELECT NOW()", connection);
            var serverTime = await command.ExecuteScalarAsync(cancellationToken);

            return DatabaseConnectionResult.Success($"Connected to PostgreSQL {version} (server time {serverTime}).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to connect to PostgreSQL.");
            return DatabaseConnectionResult.Failure($"Connection failed: {ex.Message}");
        }
    }
}

public record DatabaseConnectionResult(bool IsSuccessful, string Message)
{
    public static DatabaseConnectionResult Success(string message) => new(true, message);
    public static DatabaseConnectionResult Failure(string message) => new(false, message);
}
