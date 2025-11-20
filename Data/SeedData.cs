using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using t12Project.Models;

namespace t12Project.Data;

public static class SeedData
{
    private static readonly string[] Roles = ["Admin", "Driver", "Customer"];

    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var scopedProvider = scope.ServiceProvider;
        var context = scopedProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync();

        var roleManager = scopedProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var userManager = scopedProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await EnsureAdminAsync(userManager);
        await EnsureSampleDriverAsync(userManager);
        await EnsureSampleCustomerAsync(userManager);
    }

    private static async Task EnsureAdminAsync(UserManager<ApplicationUser> userManager)
    {
        const string adminEmail = "admin@loadhitch.com";
        var admin = await userManager.FindByEmailAsync(adminEmail);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FullName = "LoadHitch Admin",
                AccountType = AccountRole.Admin
            };
            await userManager.CreateAsync(admin, "Admin@123456!");
        }

        if (!await userManager.IsInRoleAsync(admin, "Admin"))
        {
            await userManager.AddToRoleAsync(admin, "Admin");
        }
    }

    private static async Task EnsureSampleDriverAsync(UserManager<ApplicationUser> userManager)
    {
        const string email = "driver@loadhitch.com";
        var driver = await userManager.FindByEmailAsync(email);
        if (driver is null)
        {
            driver = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = "Sample Driver",
                AccountType = AccountRole.Driver
            };
            await userManager.CreateAsync(driver, "Driver@123456!");
        }

        if (!await userManager.IsInRoleAsync(driver, "Driver"))
        {
            await userManager.AddToRoleAsync(driver, "Driver");
        }
    }

    private static async Task EnsureSampleCustomerAsync(UserManager<ApplicationUser> userManager)
    {
        const string email = "customer@loadhitch.com";
        var customer = await userManager.FindByEmailAsync(email);
        if (customer is null)
        {
            customer = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = "Sample Customer",
                AccountType = AccountRole.Customer
            };
            await userManager.CreateAsync(customer, "Customer@123456!");
        }

        if (!await userManager.IsInRoleAsync(customer, "Customer"))
        {
            await userManager.AddToRoleAsync(customer, "Customer");
        }
    }
}
