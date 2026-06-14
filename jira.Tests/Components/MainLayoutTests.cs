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
        Services.AddSingleton<BoardStateService>(new Mock<BoardStateService>().Object);

        var dbOpts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        Services.AddSingleton<AppDbContext>(new AppDbContext(dbOpts));

        var httpClient = new HttpClient(new Mock<HttpMessageHandler>().Object);
        Services.AddSingleton<WeatherService>(new WeatherService(httpClient));
    }

    [Fact]
    public void MainLayout_BodyElementIsVisible_WhenUserIsAuthorized()
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("testUser");
        authContext.SetClaims(
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Name, "testUser"),
            new Claim(ClaimTypes.Email, "test@example.com")
        );

        var cut = Render<MainLayout>(parameters => parameters
            .Add(p => p.Body,
                (RenderFragment)(builder =>
                    builder.AddMarkupContent(0, "<div id='test-content'>Main Body Content</div>")))
        );

        var bodyWrapper = cut.Find("#test-content");
        Assert.NotNull(bodyWrapper);
        Assert.Equal("Main Body Content", bodyWrapper.TextContent);
    }
}