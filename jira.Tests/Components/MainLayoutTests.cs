using Bunit;
using Bunit.TestDoubles;
using jira.Components.Layout;
using jira.Data;
using jira.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace jira.Tests.Components.Layout;

public class MainLayoutTests : BunitContext
{
    public MainLayoutTests()
    {
        // 1. Rejestracja serwisów (minimalny zestaw pod bUnit)
        Services.AddSingleton<BoardStateService>(new Mock<BoardStateService>().Object);
        
        var dbOpts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        Services.AddSingleton<AppDbContext>(new AppDbContext(dbOpts));

        // Rejestracja WeatherService z dummy HttpClient
        var httpClient = new HttpClient(new Mock<HttpMessageHandler>().Object);
        Services.AddSingleton<WeatherService>(new WeatherService(httpClient));
    }

    [Fact]
    public void MainLayout_RendersWithoutException_WhenUserIsAuthorized()
    {
        // Arrange
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("testUser");
        authContext.SetClaims(
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Name, "testUser"),
            new Claim(ClaimTypes.Email, "test@example.com")
        );

        // Act
        var cut = Render<MainLayout>(parameters => parameters
            .Add(p => p.Body, (RenderFragment)(builder => builder.AddMarkupContent(0, "<div id='test-content'>Main Body Content</div>")))
        );

        // Assert
        Assert.NotNull(cut);
    }

    [Fact]
    public void MainLayout_BodyElementIsVisible_WhenUserIsAuthorized()
    {
        // Arrange
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("testUser");
        authContext.SetClaims(
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Name, "testUser"),
            new Claim(ClaimTypes.Email, "test@example.com")
        );

        // Act
        var cut = Render<MainLayout>(parameters => parameters
            .Add(p => p.Body, (RenderFragment)(builder => builder.AddMarkupContent(0, "<div id='test-content'>Main Body Content</div>")))
        );

        // Assert
        var bodyWrapper = cut.Find("#test-content");
        Assert.NotNull(bodyWrapper);
        Assert.Equal("Main Body Content", bodyWrapper.TextContent);
    }
}