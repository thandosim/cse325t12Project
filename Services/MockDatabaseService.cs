namespace t12Project.Services;

public class MockDatabaseService
{
    public Task<(bool Success, string Message)> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult((true, "✓ Connected to Mock Database"));
    }
}
