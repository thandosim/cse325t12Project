using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using t12Project.Contracts.Auth;

namespace t12Project.Tests;

public class AuthIntegrationTests : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;

    public AuthIntegrationTests(IntegrationTestFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("admin@loadhitch.com", "Admin@123456!")]
    [InlineData("driver@loadhitch.com", "Driver@123456!")]
    [InlineData("customer@loadhitch.com", "Customer@123456!")]
    public async Task Login_ShouldReturnTokens_ForSeededUsers(string email, string password)
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = password,
            RememberMe = true
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>();
        payload.Should().NotBeNull();
        payload!.AccessToken.Should().NotBeNullOrWhiteSpace();
        payload.RefreshToken.Should().NotBeNullOrWhiteSpace();
        payload.Email.Should().Be(email);
    }

    [Fact]
    public async Task Login_ShouldFail_ForBadPassword()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "admin@loadhitch.com",
            Password = "wrong",
            RememberMe = false
        });

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.BadRequest);
    }
}
