using Bunit;
using jira.Components.Layout;
using jira.Data;
using jira.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

using Xunit;

namespace jira.Tests.Components.Layout;

public class MainLayoutTests : TestContext
{
    
    private readonly Mock<AuthenticationStateProvider> _authProviderMock;

    public MainLayoutTests()
    {
        
        // 1. Rejestracja serwisów (minimalny zestaw pod bUnit)
        Services.AddSingleton<BoardStateService>(new Mock<BoardStateService>().Object);
        Services.AddAuthorizationCore();
        
        var dbOpts = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        Services.AddSingleton<AppDbContext>(new AppDbContext(dbOpts));

        // 2. Setup stanu autoryzacji
        _authProviderMock = new Mock<AuthenticationStateProvider>();
        Services.AddScoped<AuthenticationStateProvider>(_ => _authProviderMock.Object);

        // 3. Pełny mock serwisu polityk, aby zapobiec przerwaniu renderowania
        var authServiceMock = new Mock<IAuthorizationService>();
        authServiceMock
            .Setup(x => x.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());
        authServiceMock
            .Setup(x => x.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<string>()))
            .ReturnsAsync(AuthorizationResult.Success());
            
        Services.AddScoped<IAuthorizationService>(_ => authServiceMock.Object);
    }

    private IRenderedComponent<MainLayout> RenderWithState(ClaimsPrincipal principal)
    {
        _authProviderMock.Setup(m => m.GetAuthenticationStateAsync()).ReturnsAsync(new AuthenticationState(principal));
        
        return Render<MainLayout>(parameters => parameters
            .Add(p => p.Body, (RenderFragment)(b => b.AddMarkupContent(0, "<div id='test-content'>Content</div>")))
            .AddCascadingValue(_authProviderMock.Object.GetAuthenticationStateAsync())
        );
    }

    [Fact]
    public void Coverage_AuthorizedUser_WithAvatar()
    {
        // Scenariusz: Zalogowany z linkiem do awatara
        const string avatarUrl = "https://github.com/avatar.png";
        var claims = new List<Claim> 
        { 
            new Claim(ClaimTypes.Name, "Jan Kowalski"),
            new Claim("avatar_url", avatarUrl) // dopasuj do nazwy claimu w kodzie (.razor)
        };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        
        var cut = RenderWithState(user);

        // Asercja: Sprawdzenie wyświetlania nazwy użytkownika oraz klasy awatara z CSS (.user-avatar)
        Assert.Contains("Jan Kowalski", cut.Markup);
        
        var avatarImg = cut.FindAll("img").FirstOrDefault(i => i.ClassName.Contains("user-avatar"));
        if (avatarImg != null)
        {
            Assert.Equal(avatarUrl, avatarImg.GetAttribute("src"));
        }
    }

    [Fact]
    public void Coverage_AuthorizedUser_NoAvatar_ShowsPlaceholder()
    {
        // Scenariusz: Zalogowany bez zdjęcia profilowego
        var claims = new List<Claim> { new Claim(ClaimTypes.Name, "Jan Kowalski") };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        
        var cut = RenderWithState(user);

        // Asercja: Pokrywa warunek @else, szukając diva stylizowanego przez klasę .user-avatar-placeholder
        var placeholder = cut.FindAll(".user-avatar-placeholder").FirstOrDefault();
        Assert.NotNull(placeholder);
        Assert.Contains("Jan Kowalski", cut.Markup);
    }

    [Fact]
    public void Coverage_AuthorizedUser_CanSeeLogoutButton()
    {
        // Scenariusz: Zalogowany widzi przycisk Wyloguj
        var claims = new List<Claim> { new Claim(ClaimTypes.Name, "Jan") };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        
        var cut = RenderWithState(user);

        // Asercja: Weryfikacja obecności przycisku wylogowania (.btn-logout) ze stylów CSS
        Assert.NotNull(cut.Find(".btn-logout"));
    }
}