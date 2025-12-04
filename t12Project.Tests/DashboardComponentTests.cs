using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using t12Project.Components.Pages.Dashboard;

namespace t12Project.Tests;

public class DashboardComponentTests : TestContext
{
    [Fact]
    public void CustomerDashboard_RendersForCustomerRole()
    {
        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("customer");
        auth.SetRoles("Customer");

        var cut = RenderComponent<CustomerDashboard>();

        cut.Markup.Should().Contain("Customer Portal");
        cut.Find("h1").TextContent.Should().Contain("Welcome to your Dashboard");
    }

    [Fact]
    public void DriverDashboard_RendersForDriverRole()
    {
        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("driver");
        auth.SetRoles("Driver");

        var cut = RenderComponent<DriverDashboard>();

        cut.Markup.Should().Contain("Driver Portal");
        cut.Find("h1").TextContent.Should().Contain("Welcome to your Dashboard");
    }
}
