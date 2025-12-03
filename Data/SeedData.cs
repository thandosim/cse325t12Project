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
        var drivers = await EnsureSampleDriversAsync(userManager);
        var customers = await EnsureSampleCustomersAsync(userManager);

        await EnsureSampleTrucksAsync(context, drivers);
        await EnsureSampleLoadsAsync(context, customers);

        await context.SaveChangesAsync();
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

    private static async Task<List<ApplicationUser>> EnsureSampleDriversAsync(UserManager<ApplicationUser> userManager)
    {
        var driverEmails = new[] { "driver1@loadhitch.com", "driver2@loadhitch.com", "driver3@loadhitch.com", "driver4@loadhitch.com" };
        var drivers = new List<ApplicationUser>();

        foreach (var email in driverEmails)
        {
            var driver = await userManager.FindByEmailAsync(email);
            if (driver is null)
            {
                driver = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FullName = $"Sample {email.Split('@')[0]}",
                    AccountType = AccountRole.Driver
                };
                await userManager.CreateAsync(driver, "Driver@123456!");
            }

            if (!await userManager.IsInRoleAsync(driver, "Driver"))
            {
                await userManager.AddToRoleAsync(driver, "Driver");
            }

            drivers.Add(driver);
        }

        return drivers;
    }

    private static async Task<List<ApplicationUser>> EnsureSampleCustomersAsync(UserManager<ApplicationUser> userManager)
    {
        var customerEmails = new[] { "customer1@loadhitch.com", "customer2@loadhitch.com", "customer3@loadhitch.com" };
        var customers = new List<ApplicationUser>();

        foreach (var email in customerEmails)
        {
            var customer = await userManager.FindByEmailAsync(email);
            if (customer is null)
            {
                customer = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FullName = $"Sample {email.Split('@')[0]}",
                    AccountType = AccountRole.Customer
                };
                await userManager.CreateAsync(customer, "Customer@123456!");
            }

            if (!await userManager.IsInRoleAsync(customer, "Customer"))
            {
                await userManager.AddToRoleAsync(customer, "Customer");
            }

            customers.Add(customer);
        }

        return customers;
    }

    private static async Task EnsureSampleTrucksAsync(ApplicationDbContext context, List<ApplicationUser> drivers)
    {
        if (!context.Trucks.Any())
        {
            context.Trucks.AddRange(
                new Truck { DriverId = drivers[0].Id, Name = "Truck T001", EquipmentType = "Flatbed", CapacityLbs = 20000, IsActive = true },
                new Truck { DriverId = drivers[1].Id, Name = "Truck T002", EquipmentType = "Box Truck", CapacityLbs = 15000, IsActive = true },
                new Truck { DriverId = drivers[2].Id, Name = "Truck T003", EquipmentType = "Tanker", CapacityLbs = 25000, IsActive = true },
                new Truck { DriverId = drivers[3].Id, Name = "Truck T004", EquipmentType = "Refrigerated", CapacityLbs = 18000, IsActive = true }
            );
        }
    }

    private static async Task EnsureSampleLoadsAsync(ApplicationDbContext context, List<ApplicationUser> customers)
    {
        if (!context.Loads.Any())
        {
            context.Loads.AddRange(
                new Load
                {
                    Title = "Fragile Electronics",
                    Status = "Pending",
                    PickupLocation = "Mbabane",
                    DropoffLocation = "Manzini",
                    PickupDate = DateTimeOffset.UtcNow.AddDays(1),
                    WeightLbs = 1200,
                    Description = "Fragile boxed electronics, handle with care",
                    CargoType = "Fragile",
                    CustomerId = customers[0].Id
                },
                new Load
                {
                    Title = "Steel Beams",
                    Status = "Draft",
                    PickupLocation = "Sidwashini",
                    DropoffLocation = "Nhlangano",
                    PickupDate = DateTimeOffset.UtcNow.AddDays(2),
                    WeightLbs = 3000,
                    Description = "Heavy steel beams, flatbed required",
                    CargoType = "Flatbed",
                    CustomerId = customers[0].Id
                },
                new Load
                {
                    Title = "Liquid Chemicals",
                    Status = "Available",
                    PickupLocation = "Ezulwini",
                    DropoffLocation = "Big Bend",
                    PickupDate = DateTimeOffset.UtcNow.AddDays(3),
                    WeightLbs = 5000,
                    Description = "500L liquid chemicals, hazmat handling required",
                    CargoType = "Liquid",
                    CustomerId = customers[1].Id
                },
                new Load
                {
                    Title = "Frozen Food",
                    Status = "Available",
                    PickupLocation = "Manzini",
                    DropoffLocation = "Siteki",
                    PickupDate = DateTimeOffset.UtcNow.AddDays(1),
                    WeightLbs = 2000,
                    Description = "Frozen goods, refrigerated truck required",
                    CargoType = "Refrigerated",
                    CustomerId = customers[2].Id
                },
                new Load
                {
                    Title = "Furniture",
                    Status = "Pending",
                    PickupLocation = "Mbabane",
                    DropoffLocation = "Piggs Peak",
                    PickupDate = DateTimeOffset.UtcNow.AddDays(4),
                    WeightLbs = 2500,
                    Description = "Large furniture items, boxed and palletized",
                    CargoType = "General",
                    CustomerId = customers[2].Id
                }
            );
        }
    }

    private static async Task EnsureSampleLocationUpdatesAsync(
        ApplicationDbContext context,
        List<ApplicationUser> drivers,
        List<ApplicationUser> customers)
    {
        if (!context.LocationUpdates.Any())
        {
            // Driver locations (match trucks)
            context.LocationUpdates.AddRange(
                new LocationUpdate { DriverId = drivers[0].Id, Latitude = -26.3167m, Longitude = 31.1333m, Notes = "Truck T001 in Mbabane" },
                new LocationUpdate { DriverId = drivers[1].Id, Latitude = -26.4833m, Longitude = 31.3667m, Notes = "Truck T002 in Manzini" },
                new LocationUpdate { DriverId = drivers[2].Id, Latitude = -26.4333m, Longitude = 31.2000m, Notes = "Truck T003 in Ezulwini" },
                new LocationUpdate { DriverId = drivers[3].Id, Latitude = -25.9667m, Longitude = 31.2500m, Notes = "Truck T004 in Piggs Peak" }
            );
        }

        if (!context.CustomerLocationUpdates.Any())
        {
            // Customer load locations
            context.CustomerLocationUpdates.AddRange(
                new CustomerLocationUpdate { CustomerId = customers[0].Id, Latitude = -26.3167m, Longitude = 31.1333m, Notes = "Load pickup in Mbabane" },
                new CustomerLocationUpdate { CustomerId = customers[0].Id, Latitude = -27.1167m, Longitude = 31.2000m, Notes = "Load dropoff in Nhlangano" },
                new CustomerLocationUpdate { CustomerId = customers[1].Id, Latitude = -26.8167m, Longitude = 31.9333m, Notes = "Load pickup in Big Bend" },
                new CustomerLocationUpdate { CustomerId = customers[2].Id, Latitude = -26.4500m, Longitude = 31.9500m, Notes = "Load dropoff in Siteki" },
                new CustomerLocationUpdate { CustomerId = customers[2].Id, Latitude = -26.4833m, Longitude = 31.3667m, Notes = "Load pickup in Manzini" }
            );
        }

        await context.SaveChangesAsync();
    }


}
