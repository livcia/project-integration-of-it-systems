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
        // 1. Arrange: Uzyskaj dostęp do FakeNavigationManager
        var nav = Services.GetRequiredService<NavigationManager>();

        // 2. Act: Wyrenderuj komponent za pomocą metody Render
        // Jest to zalecany sposób w nowszych wersjach bUnit
        Render<RedirectToLogin>();

        // 3. Assert: Sprawdź, czy adres się zmienił
        Assert.Equal("login", nav.ToBaseRelativePath(nav.Uri));
    }
}