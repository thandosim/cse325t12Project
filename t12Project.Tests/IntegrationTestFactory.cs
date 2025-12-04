using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using t12Project.Data;

namespace t12Project.Tests;

public class IntegrationTestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTests");

        // Ensure required config is present
        Environment.SetEnvironmentVariable("Jwt__SigningKey", "integration-test-signing-key-1234567890");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "integration-tests");
        Environment.SetEnvironmentVariable("Jwt__Audience", "integration-tests");
        Environment.SetEnvironmentVariable("Authentication__Google__ClientId", "dummy-client-id");
        Environment.SetEnvironmentVariable("Authentication__Google__ClientSecret", "dummy-client-secret");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = "integration-test-signing-key-1234567890",
                ["Jwt:Issuer"] = "integration-tests",
                ["Jwt:Audience"] = "integration-tests",
                ["Authentication:Google:ClientId"] = "dummy-client-id",
                ["Authentication:Google:ClientSecret"] = "dummy-client-secret"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace the db context with an in-memory database for tests
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase("IntegrationTestsDb"));

            // Build the service provider to ensure the database is created/seeding runs
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var scopedServices = scope.ServiceProvider;
            var db = scopedServices.GetRequiredService<ApplicationDbContext>();
            db.Database.EnsureCreated();
        });
    }
}
