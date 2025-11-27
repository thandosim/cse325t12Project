using System.Threading.Tasks;

namespace t12Project.Services;

// Very small in-memory account store for development/testing
public class MockAccountService : IAccountService
{
    private AccountDto? _account;

    public Task<AccountDto?> GetAsync()
    {
        return Task.FromResult(_account);
    }

    public Task SaveAsync(AccountDto account)
    {
        _account = account;
        return Task.CompletedTask;
    }
}
