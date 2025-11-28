using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Moq;
using Microsoft.Extensions.Logging;
using t12Project.Data;
using t12Project.Models;
using t12Project.Services;

namespace t12Project.Tests;

public class AdminUserServiceTests
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ApplicationDbContext _dbContext;
    private readonly Mock<IHttpContextAccessor> _httpContextAccessor = new();
    private readonly AdminUserService _service;

    public AdminUserServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ApplicationDbContext(options);

        var roleStore = new RoleStore<IdentityRole>(_dbContext);
        _roleManager = new RoleManager<IdentityRole>(roleStore, new[] { new RoleValidator<IdentityRole>() },
            new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), null);
        foreach (var role in Enum.GetNames<AccountRole>())
        {
            _roleManager.CreateAsync(new IdentityRole(role)).GetAwaiter().GetResult();
        }

        var userStore = new UserStore<ApplicationUser, IdentityRole, ApplicationDbContext>(_dbContext);
        _userManager = TestUserManager(userStore);

        _service = new AdminUserService(_userManager, _dbContext, _httpContextAccessor.Object);
    }

    [Fact]
    public async Task DeleteUserAsync_ShouldPreventDeletingSelf()
    {
        var admin = await SeedUserAsync("self@admin.com", AccountRole.Admin);
        SetCurrentUser(admin.Id);

        var result = await _service.DeleteUserAsync(admin.Id);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("cannot delete your own");
    }

    [Fact]
    public async Task UpdateRoleAsync_ShouldPreventDemotingLastAdmin()
    {
        var admin = await SeedUserAsync("onlyadmin@site.com", AccountRole.Admin);
        SetCurrentUser("actor");

        var result = await _service.UpdateRoleAsync(admin.Id, AccountRole.Customer);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("last remaining admin");
    }

    [Fact]
    public async Task BlockUserAsync_ShouldPreventBlockingAdmin()
    {
        var admin = await SeedUserAsync("block@admin.com", AccountRole.Admin);
        var result = await _service.BlockUserAsync(admin.Id);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("cannot be blocked");
    }

    [Fact]
    public async Task GetUsersAsync_ShouldSupportPagingAndSearch()
    {
        await SeedUserAsync("alice@example.com", AccountRole.Customer, "Alice");
        await SeedUserAsync("bob@example.com", AccountRole.Driver, "Bob");
        await SeedUserAsync("carol@example.com", AccountRole.Customer, "Carol");

        var page1 = await _service.GetUsersAsync(page: 1, pageSize: 2, search: "example");
        var page2 = await _service.GetUsersAsync(page: 2, pageSize: 2, search: "example");

        page1.TotalCount.Should().Be(3);
        page1.Items.Should().HaveCount(2);
        page2.Items.Should().HaveCount(1);
        page1.Items.Select(u => u.Email).Should().Contain("alice@example.com");
    }

    [Fact]
    public async Task Actions_ShouldWriteActivityLog()
    {
        var actor = await SeedUserAsync("actor@admin.com", AccountRole.Admin);
        SetCurrentUser(actor.Id);
        var target = await SeedUserAsync("target@user.com", AccountRole.Customer);

        await _service.BlockUserAsync(target.Id);
        await _service.UnblockUserAsync(target.Id);
        await _service.UpdateRoleAsync(target.Id, AccountRole.Driver);

        _dbContext.ActivityLogs.Count().Should().BeGreaterThanOrEqualTo(3);
    }

    private async Task<ApplicationUser> SeedUserAsync(string email, AccountRole role, string? name = null)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            Email = email,
            UserName = email,
            NormalizedEmail = email.ToUpperInvariant(),
            NormalizedUserName = email.ToUpperInvariant(),
            FullName = name ?? email,
            AccountType = role
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        await _userManager.AddToRoleAsync(user, role.ToString());
        return user;
    }

    private void SetCurrentUser(string userId)
    {
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "Test");
        var principal = new ClaimsPrincipal(identity);
        _httpContextAccessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext { User = principal });
    }

    private static UserManager<ApplicationUser> TestUserManager(IUserStore<ApplicationUser> store)
    {
        var options = new Mock<Microsoft.Extensions.Options.IOptions<IdentityOptions>>();
        options.Setup(o => o.Value).Returns(new IdentityOptions());
        var userValidators = new List<IUserValidator<ApplicationUser>>();
        var validator = new Mock<IUserValidator<ApplicationUser>>();
        validator.Setup(v => v.ValidateAsync(It.IsAny<UserManager<ApplicationUser>>(), It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);
        userValidators.Add(validator.Object);
        var pwdValidators = new List<IPasswordValidator<ApplicationUser>> { new PasswordValidator<ApplicationUser>() };

        return new UserManager<ApplicationUser>(store, options.Object, new PasswordHasher<ApplicationUser>(),
            userValidators, pwdValidators, new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(),
            null, new Mock<Microsoft.Extensions.Logging.ILogger<UserManager<ApplicationUser>>>().Object);
    }
}
