using Bunit;
using jira.Components.Layout;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class RedirectToLoginTests : TestContext
{
    [Fact]
    public void RedirectLogin_ShouldNavigateToLogin_OnInitialized()
    {
        var nav = Services.GetRequiredService<NavigationManager>();

        Render<RedirectToLogin>();

        Assert.Equal("login", nav.ToBaseRelativePath(nav.Uri));
    }
}