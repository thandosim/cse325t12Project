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
        if (context.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true)
        {
            await context.Database.EnsureCreatedAsync();
        }
        else
        {
            await context.Database.MigrateAsync();
        }

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
        await EnsureSampleDriverRoutesAsync(context, drivers);

        // 🚀 Added: seed location updates for drivers and customers
        await EnsureSampleLocationUpdatesAsync(context, drivers, customers);

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
                },
                // Cape Town Loads
                new Load
                {
                    Title = "Wine Barrels",
                    Status = "Available",
                    PickupLocation = "Stellenbosch, Cape Town",
                    DropoffLocation = "Johannesburg",
                    PickupLatitude = -33.9321m,
                    PickupLongitude = 18.8602m,
                    DropoffLatitude = -26.2041m,
                    DropoffLongitude = 28.0473m,
                    PickupDate = DateTimeOffset.UtcNow.AddDays(2),
                    WeightLbs = 8000,
                    Description = "Premium wine barrels from Stellenbosch vineyards",
                    CargoType = "General",
                    CustomerId = customers[0].Id
                },
                new Load
                {
                    Title = "Fresh Seafood",
                    Status = "Available",
                    PickupLocation = "Cape Town Harbour",
                    DropoffLocation = "Durban",
                    PickupLatitude = -33.9062m,
                    PickupLongitude = 18.4232m,
                    DropoffLatitude = -29.8587m,
                    DropoffLongitude = 31.0218m,
                    PickupDate = DateTimeOffset.UtcNow.AddDays(1),
                    WeightLbs = 3500,
                    Description = "Fresh catch from Cape Town harbour, refrigerated transport required",
                    CargoType = "Refrigerated",
                    CustomerId = customers[1].Id
                },
                new Load
                {
                    Title = "IT Equipment",
                    Status = "Available",
                    PickupLocation = "Century City, Cape Town",
                    DropoffLocation = "Pretoria",
                    PickupLatitude = -33.8891m,
                    PickupLongitude = 18.5126m,
                    DropoffLatitude = -25.7479m,
                    DropoffLongitude = 28.2293m,
                    PickupDate = DateTimeOffset.UtcNow.AddDays(3),
                    WeightLbs = 1500,
                    Description = "Server racks and networking equipment, fragile handling required",
                    CargoType = "Fragile",
                    CustomerId = customers[0].Id
                },
                new Load
                {
                    Title = "Construction Materials",
                    Status = "Available",
                    PickupLocation = "Bellville, Cape Town",
                    DropoffLocation = "George",
                    PickupLatitude = -33.9000m,
                    PickupLongitude = 18.6333m,
                    DropoffLatitude = -33.9631m,
                    DropoffLongitude = 22.4617m,
                    PickupDate = DateTimeOffset.UtcNow.AddDays(2),
                    WeightLbs = 15000,
                    Description = "Building materials including cement bags and steel rebar",
                    CargoType = "Flatbed",
                    CustomerId = customers[2].Id
                },
                new Load
                {
                    Title = "Automotive Parts",
                    Status = "Available",
                    PickupLocation = "Paarden Eiland, Cape Town",
                    DropoffLocation = "Port Elizabeth",
                    PickupLatitude = -33.9167m,
                    PickupLongitude = 18.4500m,
                    DropoffLatitude = -33.9608m,
                    DropoffLongitude = 25.6022m,
                    PickupDate = DateTimeOffset.UtcNow.AddDays(1),
                    WeightLbs = 4500,
                    Description = "Replacement auto parts for dealership network",
                    CargoType = "General",
                    CustomerId = customers[1].Id
                }
            );
        }
    }

    private static async Task EnsureSampleDriverRoutesAsync(ApplicationDbContext context, List<ApplicationUser> drivers)
    {
        if (!context.DriverRoutes.Any())
        {
            context.DriverRoutes.AddRange(
                // Cape Town routes
                new DriverRoute
                {
                    DriverId = drivers[0].Id,
                    StartLocation = "Cape Town CBD",
                    EndLocation = "Johannesburg",
                    StartLatitude = -33.9249m,
                    StartLongitude = 18.4241m,
                    EndLatitude = -26.2041m,
                    EndLongitude = 28.0473m,
                    AvailableFrom = DateTimeOffset.UtcNow,
                    AvailableTo = DateTimeOffset.UtcNow.AddDays(14),
                    EquipmentType = "Flatbed",
                    MaxWeightLbs = 20000,
                    IsActive = true,
                    Notes = "Weekly route from Cape Town to Joburg, can stop in Bloemfontein"
                },
                new DriverRoute
                {
                    DriverId = drivers[1].Id,
                    StartLocation = "Stellenbosch",
                    EndLocation = "Durban",
                    StartLatitude = -33.9321m,
                    StartLongitude = 18.8602m,
                    EndLatitude = -29.8587m,
                    EndLongitude = 31.0218m,
                    AvailableFrom = DateTimeOffset.UtcNow,
                    AvailableTo = DateTimeOffset.UtcNow.AddDays(7),
                    EquipmentType = "Refrigerated",
                    MaxWeightLbs = 15000,
                    IsActive = true,
                    Notes = "Refrigerated truck, ideal for wine and perishables"
                },
                new DriverRoute
                {
                    DriverId = drivers[2].Id,
                    StartLocation = "Cape Town Harbour",
                    EndLocation = "Port Elizabeth",
                    StartLatitude = -33.9062m,
                    StartLongitude = 18.4232m,
                    EndLatitude = -33.9608m,
                    EndLongitude = 25.6022m,
                    AvailableFrom = DateTimeOffset.UtcNow.AddDays(1),
                    AvailableTo = DateTimeOffset.UtcNow.AddDays(10),
                    EquipmentType = "Box Truck",
                    MaxWeightLbs = 12000,
                    IsActive = true,
                    Notes = "Coastal route via Garden Route, scenic stops available"
                },
                new DriverRoute
                {
                    DriverId = drivers[3].Id,
                    StartLocation = "Century City, Cape Town",
                    EndLocation = "Pretoria",
                    StartLatitude = -33.8891m,
                    StartLongitude = 18.5126m,
                    EndLatitude = -25.7479m,
                    EndLongitude = 28.2293m,
                    AvailableFrom = DateTimeOffset.UtcNow,
                    AvailableTo = DateTimeOffset.UtcNow.AddDays(5),
                    EquipmentType = "General",
                    MaxWeightLbs = 18000,
                    IsActive = true,
                    Notes = "Express route to Pretoria, 2-day delivery"
                },
                new DriverRoute
                {
                    DriverId = drivers[0].Id,
                    StartLocation = "Bellville, Cape Town",
                    EndLocation = "George",
                    StartLatitude = -33.9000m,
                    StartLongitude = 18.6333m,
                    EndLatitude = -33.9631m,
                    EndLongitude = 22.4617m,
                    AvailableFrom = DateTimeOffset.UtcNow.AddDays(2),
                    AvailableTo = DateTimeOffset.UtcNow.AddDays(8),
                    EquipmentType = "Flatbed",
                    MaxWeightLbs = 25000,
                    IsActive = true,
                    Notes = "Heavy haul capacity, construction materials welcome"
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
