using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using t12Project.Components.Pages.Admin;

namespace t12Project.Tests;

public class AdminDashboardUiTests : TestContext
{
    [Fact(Skip = "Requires auth wiring; placeholder to ensure bUnit setup compiles")]
    public void AdminDashboard_Render_Basics()
    {
        // Arrange
        Services.AddLogging();

        // Act
        var cut = RenderComponent<AdminDashboard>();

        // Assert
        cut.Markup.Should().Contain("Admin Control Center");
    }
}
