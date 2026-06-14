using Bunit;
using jira.Components.Layout;
using jira.Data;
using jira.DbModels;
using jira.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components;
using Moq;
using System.Security.Claims;
using Xunit;

namespace jira.Tests.Components;

public class NavMenuTests : BunitContext
{
    private readonly Mock<BoardStateService> _boardStateMock;

    public NavMenuTests()
    {
        _boardStateMock = new Mock<BoardStateService>();
        Services.AddSingleton<BoardStateService>(_boardStateMock.Object);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void SetupAuth(int userId)
    {
        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
            "TestAuthType");

        var authState = new AuthenticationState(new ClaimsPrincipal(identity));

        var authProviderMock = new Mock<AuthenticationStateProvider>();
        authProviderMock
            .Setup(x => x.GetAuthenticationStateAsync())
            .ReturnsAsync(authState);

        Services.AddScoped<AuthenticationStateProvider>(_ => authProviderMock.Object);
    }

    /// <summary>
    /// Tworzy izolowaną bazę InMemory i rejestruje AppDbContext BEZPOŚREDNIO
    /// w kontenerze bUnit jako Singleton.
    ///
    /// DLACZEGO TAK, NIE PRZEZ MOCK IServiceScopeFactory:
    ///   bUnit rejestruje własny IServiceScopeFactory wewnętrznie podczas budowania
    ///   ServiceProvider. Próba zastąpienia go przez AddSingleton&lt;IServiceScopeFactory&gt;
    ///   kończy się niepowodzeniem – bUnit ignoruje taką nadpisaną rejestrację.
    ///   Zamiast tego wystarczy zarejestrować AppDbContext w Services bUnit;
    ///   wbudowany IServiceScopeFactory automatycznie rozwiąże go ze scope,
    ///   gdy komponent wywoła scope.ServiceProvider.GetRequiredService&lt;AppDbContext&gt;().
    /// </summary>
    private AppDbContext CreateDb()
    {
        var dbName = Guid.NewGuid().ToString();
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var db = new AppDbContext(opts);

        // ✅ Prosta, bezpośrednia rejestracja – żadnych mocków łańcucha scope
        Services.AddSingleton<AppDbContext>(db);

        return db;
    }

    // -------------------------------------------------------------------------
    // Testy
    // -------------------------------------------------------------------------

    [Fact]
    public void NavMenu_WhenNoBoards_ShowsEmptyMessage()
    {
        // Arrange – baza pusta, user ID=1 nie ma żadnych tablic
        CreateDb();
        SetupAuth(1);

        // Act
        var cut = Render<NavMenu>();

        // Assert
        cut.WaitForAssertion(() =>
            Assert.Contains("Brak tablic", cut.Markup));
    }

    [Fact]
    public async Task NavMenu_WhenBoardsExist_DisplaysBoardLinks()
    {
        // Arrange
        var db = CreateDb();
        db.Tablice.Add(new Tablica
        {
            IdTablicy          = 1,
            NazwaTablicy       = "Testowa Tablica",
            IdUzytkownikaOwner = 1,
            TabliceUzyt        = new List<TablicaUzytkownik>()
        });
        await db.SaveChangesAsync();

        SetupAuth(1);

        // Act
        var cut = Render<NavMenu>();

        // Assert
        cut.WaitForAssertion(() =>
            Assert.Contains("Testowa Tablica",
                cut.Find(".nav-link-text").TextContent));
    }

    [Fact]
    public async Task NavMenu_OnActiveLink_AppliesActiveClass()
    {
        // Arrange
        var db = CreateDb();
        db.Tablice.Add(new Tablica
        {
            IdTablicy          = 5,
            NazwaTablicy       = "Active Board",
            IdUzytkownikaOwner = 1,
            TabliceUzyt        = new List<TablicaUzytkownik>()
        });
        await db.SaveChangesAsync();

        SetupAuth(1);

        // Nawiguj PRZED renderem – komponent odczyta URL w OnInitializedAsync
        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/board/5");

        // Act
        var cut = Render<NavMenu>();

        // Assert
        cut.WaitForAssertion(() =>
            Assert.Contains("active",
                cut.Find("a[href='/board/5']").ClassName ?? ""));
    }
}