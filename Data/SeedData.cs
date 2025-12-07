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
                new Truck
                {
                    DriverId = drivers[0].Id,
                    Name = "Mercedes Actros 2541",
                    EquipmentType = "Flatbed",
                    CapacityLbs = 20000,
                    IsActive = true
                },
                new Truck
                {
                    DriverId = drivers[1].Id,
                    Name = "Isuzu NPR Refrigerated",
                    EquipmentType = "Refrigerated",
                    CapacityLbs = 15000,
                    IsActive = true
                },
                new Truck
                {
                    DriverId = drivers[2].Id,
                    Name = "MAN TGS Box Truck",
                    EquipmentType = "Box Truck",
                    CapacityLbs = 18000,
                    IsActive = true
                },
                new Truck
                {
                    DriverId = drivers[3].Id,
                    Name = "Scania R450 Flatbed",
                    EquipmentType = "Flatbed",
                    CapacityLbs = 22000,
                    IsActive = true
                },
                new Truck
                {
                    DriverId = drivers[0].Id,
                    Name = "Volvo FH16 Heavy Duty",
                    EquipmentType = "General",
                    CapacityLbs = 25000,
                    IsActive = true
                }
            );
        }
    }

    private static async Task EnsureSampleLoadsAsync(ApplicationDbContext context, List<ApplicationUser> customers)
    {
        if (!context.Loads.Any())
        {
            context.Loads.AddRange(
                // Loads from Pinelands and surrounding Cape Town areas
                new Load
                {
                    Title = "Office Furniture Relocation",
                    Status = "Available",
                    PickupLocation = "Pinelands, Cape Town",
                    DropoffLocation = "Bellville Industrial, Cape Town",
                    PickupLatitude = -33.9349m,
                    PickupLongitude = 18.4956m,
                    DropoffLatitude = -33.9139m,
                    DropoffLongitude = 18.6289m,
                    PickupDate = DateTimeOffset.UtcNow.AddDays(1),
                    WeightLbs = 2500,
                    Description = "Complete office furniture set - desks, chairs, filing cabinets. Handle with care.",
                    CargoType = "General",
                    CustomerId = customers[0].Id
                },
                new Load
                {
                    Title = "Medical Equipment",
                    Status = "Available",
                    PickupLocation = "Ndabeni, Cape Town",
                    DropoffLocation = "Panorama Mediclinic, Cape Town",
                    PickupLatitude = -33.9356m,
                    PickupLongitude = 18.5112m,
                    DropoffLatitude = -33.8694m,
                    DropoffLongitude = 18.5686m,
                    PickupDate = DateTimeOffset.UtcNow.AddHours(6),
                    WeightLbs = 1200,
                    Description = "Fragile medical diagnostic equipment. Temperature controlled transport preferred.",
                    CargoType = "Fragile",
                    CustomerId = customers[1].Id
                },
                new Load
                {
                    Title = "Restaurant Supplies",
                    Status = "Available",
                    PickupLocation = "Maitland, Cape Town",
                    DropoffLocation = "Claremont, Cape Town",
                    PickupLatitude = -33.9442m,
                    PickupLongitude = 18.5156m,
                    DropoffLatitude = -33.9789m,
                    DropoffLongitude = 18.4644m,
                    PickupDate = DateTimeOffset.UtcNow.AddDays(2),
                    WeightLbs = 3500,
                    Description = "Industrial kitchen equipment and frozen food supplies. Refrigerated transport required.",
                    CargoType = "Refrigerated",
                    CustomerId = customers[2].Id
                },
                new Load
                {
                    Title = "Building Materials",
                    Status = "Available",
                    PickupLocation = "Goodwood, Cape Town",
                    DropoffLocation = "Thornton, Cape Town",
                    PickupLatitude = -33.9167m,
                    PickupLongitude = 18.5500m,
                    DropoffLatitude = -33.8956m,
                    DropoffLongitude = 18.5167m,
                    PickupDate = DateTimeOffset.UtcNow.AddDays(1),
                    WeightLbs = 8000,
                    Description = "Cement bags, bricks, and steel reinforcement bars. Flatbed truck required.",
                    CargoType = "Flatbed",
                    CustomerId = customers[0].Id
                },
                new Load
                {
                    Title = "Electronics Shipment",
                    Status = "Available",
                    PickupLocation = "Salt River, Cape Town",
                    DropoffLocation = "Tyger Valley, Cape Town",
                    PickupLatitude = -33.9344m,
                    PickupLongitude = 18.4511m,
                    DropoffLatitude = -33.8656m,
                    DropoffLongitude = 18.6456m,
                    PickupDate = DateTimeOffset.UtcNow.AddDays(3),
                    WeightLbs = 1800,
                    Description = "Consumer electronics - TVs, computers, tablets. Extremely fragile, insurance required.",
                    CargoType = "Fragile",
                    CustomerId = customers[1].Id
                },
                new Load
                {
                    Title = "Fresh Produce Delivery",
                    Status = "Available",
                    PickupLocation = "Epping Market, Cape Town",
                    DropoffLocation = "Gardens Centre, Cape Town",
                    PickupLatitude = -33.9167m,
                    PickupLongitude = 18.5333m,
                    DropoffLatitude = -33.9356m,
                    DropoffLongitude = 18.4108m,
                    PickupDate = DateTimeOffset.UtcNow.AddHours(12),
                    WeightLbs = 2200,
                    Description = "Fresh fruits and vegetables for restaurant. Early morning delivery preferred.",
                    CargoType = "Refrigerated",
                    CustomerId = customers[2].Id
                },
                new Load
                {
                    Title = "Gym Equipment",
                    Status = "Available",
                    PickupLocation = "Rondebosch, Cape Town",
                    DropoffLocation = "Pinelands, Cape Town",
                    PickupLatitude = -33.9633m,
                    PickupLongitude = 18.4789m,
                    DropoffLatitude = -33.9349m,
                    DropoffLongitude = 18.4956m,
                    PickupDate = DateTimeOffset.UtcNow.AddDays(2),
                    WeightLbs = 5500,
                    Description = "Commercial gym equipment - treadmills, weight machines, free weights. Heavy lift required.",
                    CargoType = "General",
                    CustomerId = customers[0].Id
                },
                new Load
                {
                    Title = "Pharmaceutical Supplies",
                    Status = "Available",
                    PickupLocation = "Brooklyn, Cape Town",
                    DropoffLocation = "N1 City, Cape Town",
                    PickupLatitude = -33.9450m,
                    PickupLongitude = 18.4733m,
                    DropoffLatitude = -33.8778m,
                    DropoffLongitude = 18.5744m,
                    PickupDate = DateTimeOffset.UtcNow.AddDays(1),
                    WeightLbs = 950,
                    Description = "Temperature-sensitive pharmaceuticals. Must maintain 2-8°C throughout transit.",
                    CargoType = "Refrigerated",
                    CustomerId = customers[1].Id
                },
                new Load
                {
                    Title = "Auto Parts Delivery",
                    Status = "Available",
                    PickupLocation = "Parow, Cape Town",
                    DropoffLocation = "Milnerton, Cape Town",
                    PickupLatitude = -33.9000m,
                    PickupLongitude = 18.5833m,
                    DropoffLatitude = -33.8767m,
                    DropoffLongitude = 18.4983m,
                    PickupDate = DateTimeOffset.UtcNow.AddDays(4),
                    WeightLbs = 3200,
                    Description = "Car engine parts and accessories for dealership. Some items fragile.",
                    CargoType = "General",
                    CustomerId = customers[2].Id
                },
                new Load
                {
                    Title = "Wine & Spirits",
                    Status = "Available",
                    PickupLocation = "Observatory, Cape Town",
                    DropoffLocation = "Sea Point, Cape Town",
                    PickupLatitude = -33.9378m,
                    PickupLongitude = 18.4728m,
                    DropoffLatitude = -33.9267m,
                    DropoffLongitude = 18.3861m,
                    PickupDate = DateTimeOffset.UtcNow.AddDays(5),
                    WeightLbs = 1600,
                    Description = "Premium wine and spirits shipment. Fragile glass bottles, careful handling essential.",
                    CargoType = "Fragile",
                    CustomerId = customers[0].Id
                },
                new Load
                {
                    Title = "Textile Shipment",
                    Status = "Available",
                    PickupLocation = "Woodstock, Cape Town",
                    DropoffLocation = "Durbanville, Cape Town",
                    PickupLatitude = -33.9308m,
                    PickupLongitude = 18.4472m,
                    DropoffLatitude = -33.8328m,
                    DropoffLongitude = 18.6489m,
                    PickupDate = DateTimeOffset.UtcNow.AddDays(3),
                    WeightLbs = 2800,
                    Description = "Fabric rolls and clothing samples for fashion retailer.",
                    CargoType = "General",
                    CustomerId = customers[1].Id
                },
                new Load
                {
                    Title = "Frozen Seafood",
                    Status = "Available",
                    PickupLocation = "Table Bay Harbour, Cape Town",
                    DropoffLocation = "Brackenfell, Cape Town",
                    PickupLatitude = -33.9062m,
                    PickupLongitude = 18.4232m,
                    DropoffLatitude = -33.8667m,
                    DropoffLongitude = 18.7000m,
                    PickupDate = DateTimeOffset.UtcNow.AddHours(8),
                    WeightLbs = 4200,
                    Description = "Fresh frozen catch from local fishing vessels. Must maintain -18°C.",
                    CargoType = "Refrigerated",
                    CustomerId = customers[2].Id
                }
            );
        }
    }

    private static async Task EnsureSampleDriverRoutesAsync(ApplicationDbContext context, List<ApplicationUser> drivers)
    {
        if (!context.DriverRoutes.Any())
        {
            context.DriverRoutes.AddRange(
                // Local Cape Town / Pinelands area routes
                new DriverRoute
                {
                    DriverId = drivers[0].Id,
                    StartLocation = "Pinelands, Cape Town",
                    EndLocation = "Bellville Industrial, Cape Town",
                    StartLatitude = -33.9349m,
                    StartLongitude = 18.4956m,
                    EndLatitude = -33.9139m,
                    EndLongitude = 18.6289m,
                    AvailableFrom = DateTimeOffset.UtcNow,
                    AvailableTo = DateTimeOffset.UtcNow.AddDays(7),
                    EquipmentType = "Flatbed",
                    MaxWeightLbs = 20000,
                    IsActive = true,
                    Notes = "Daily local route, can handle construction materials and heavy equipment"
                },
                new DriverRoute
                {
                    DriverId = drivers[1].Id,
                    StartLocation = "Maitland, Cape Town",
                    EndLocation = "Northern Suburbs, Cape Town",
                    StartLatitude = -33.9442m,
                    StartLongitude = 18.5156m,
                    EndLatitude = -33.8656m,
                    EndLongitude = 18.6456m,
                    AvailableFrom = DateTimeOffset.UtcNow,
                    AvailableTo = DateTimeOffset.UtcNow.AddDays(14),
                    EquipmentType = "Refrigerated",
                    MaxWeightLbs = 15000,
                    IsActive = true,
                    Notes = "Refrigerated transport for perishables, restaurants, and medical supplies"
                },
                new DriverRoute
                {
                    DriverId = drivers[2].Id,
                    StartLocation = "Ndabeni, Cape Town",
                    EndLocation = "Claremont, Cape Town",
                    StartLatitude = -33.9356m,
                    StartLongitude = 18.5112m,
                    EndLatitude = -33.9789m,
                    EndLongitude = 18.4644m,
                    AvailableFrom = DateTimeOffset.UtcNow,
                    AvailableTo = DateTimeOffset.UtcNow.AddDays(10),
                    EquipmentType = "Box Truck",
                    MaxWeightLbs = 18000,
                    IsActive = true,
                    Notes = "Enclosed truck for electronics, furniture, and general cargo"
                },
                new DriverRoute
                {
                    DriverId = drivers[3].Id,
                    StartLocation = "Goodwood, Cape Town",
                    EndLocation = "Brackenfell, Cape Town",
                    StartLatitude = -33.9167m,
                    StartLongitude = 18.5500m,
                    EndLatitude = -33.8667m,
                    EndLongitude = 18.7000m,
                    AvailableFrom = DateTimeOffset.UtcNow.AddDays(1),
                    AvailableTo = DateTimeOffset.UtcNow.AddDays(8),
                    EquipmentType = "General",
                    MaxWeightLbs = 22000,
                    IsActive = true,
                    Notes = "Flexible route serving industrial areas, can accommodate various cargo types"
                },
                new DriverRoute
                {
                    DriverId = drivers[0].Id,
                    StartLocation = "Salt River, Cape Town",
                    EndLocation = "Tyger Valley, Cape Town",
                    StartLatitude = -33.9344m,
                    StartLongitude = 18.4511m,
                    EndLatitude = -33.8656m,
                    EndLongitude = 18.6456m,
                    AvailableFrom = DateTimeOffset.UtcNow.AddDays(2),
                    AvailableTo = DateTimeOffset.UtcNow.AddDays(9),
                    EquipmentType = "Flatbed",
                    MaxWeightLbs = 25000,
                    IsActive = true,
                    Notes = "Heavy-duty flatbed for building materials and oversized cargo"
                },
                new DriverRoute
                {
                    DriverId = drivers[1].Id,
                    StartLocation = "Table Bay Harbour, Cape Town",
                    EndLocation = "Durbanville, Cape Town",
                    StartLatitude = -33.9062m,
                    StartLongitude = 18.4232m,
                    EndLatitude = -33.8328m,
                    EndLongitude = 18.6489m,
                    AvailableFrom = DateTimeOffset.UtcNow,
                    AvailableTo = DateTimeOffset.UtcNow.AddDays(5),
                    EquipmentType = "Refrigerated",
                    MaxWeightLbs = 15000,
                    IsActive = true,
                    Notes = "Seafood and frozen goods specialist route from harbour"
                },
                new DriverRoute
                {
                    DriverId = drivers[0].Id,
                    StartLocation = "Durban",
                    EndLocation = "Pretoria",
                    StartLatitude = -29.8587m,
                    StartLongitude = 31.0218m,
                    EndLatitude = -25.7479m,
                    EndLongitude = 28.2293m,
                    AvailableFrom = DateTimeOffset.UtcNow.AddDays(1),
                    AvailableTo = DateTimeOffset.UtcNow.AddDays(6),
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
