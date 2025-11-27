using System.Threading.Tasks;

namespace t12Project.Services;

public interface IAccountService
{
    Task<AccountDto?> GetAsync();
    Task SaveAsync(AccountDto account);
}

public record AccountDto(string? FirstName, string? LastName, string? Email, string? Phone);
