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
    
    [Fact]
    public async Task NavMenu_RefreshesList_WhenBoardStateServiceNotifies()
    {
        // Arrange
        var db = CreateDb();
        SetupAuth(1);
    
        // Używamy prawdziwego serwisu zamiast mocka, aby obsłużyć realny Event Bus
        var boardState = new BoardStateService();
        Services.AddSingleton(boardState); 

        var cut = Render<NavMenu>();

        // Assert: Brak tablic na początku
        Assert.Contains("Brak tablic", cut.Markup);

        // Act: Dodajemy tablicę do DB i wywołujemy powiadomienie
        db.Tablice.Add(new Tablica { IdTablicy = 10, NazwaTablicy = "Nowa Tablica", IdUzytkownikaOwner = 1 });
        await db.SaveChangesAsync();
    
        await boardState.NotifyBoardCreatedAsync();

        // Assert: Lista powinna się zaktualizować (WaitForState czeka na zmianę w DOM)
        cut.WaitForState(() => cut.Markup.Contains("Nowa Tablica"));
        Assert.DoesNotContain("Brak tablic", cut.Markup);
    }
    
    [Fact]
    public async Task NavMenu_OnlyShowsUserOwnBoards()
    {
        // Arrange
        var db = CreateDb();
        // Tablica użytkownika 1
        db.Tablice.Add(new Tablica { IdTablicy = 1, NazwaTablicy = "Moja", IdUzytkownikaOwner = 1 });
        // Tablica użytkownika 2
        db.Tablice.Add(new Tablica { IdTablicy = 2, NazwaTablicy = "Obca", IdUzytkownikaOwner = 2 });
        await db.SaveChangesAsync();

        SetupAuth(1); // Zalogowany użytkownik 1

        // Act
        var cut = Render<NavMenu>();

        // Assert
        cut.WaitForAssertion(() => {
            Assert.Contains("Moja", cut.Markup);
            Assert.DoesNotContain("Obca", cut.Markup);
        });
    }

    // -------------------------------------------------------------------------
    // Pomocnik – auth przez Email (bez parsowanego NameIdentifier)
    // -------------------------------------------------------------------------

    private void SetupAuthWithEmail(string email)
    {
        var claims = new List<Claim>
        {
            // NameIdentifier celowo nieparsowany (nie-int) → fallback na Email
            new Claim(ClaimTypes.NameIdentifier, "not-an-int"),
            new Claim(ClaimTypes.Email, email),
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var authState = new AuthenticationState(new ClaimsPrincipal(identity));

        var authProviderMock = new Mock<AuthenticationStateProvider>();
        authProviderMock
            .Setup(x => x.GetAuthenticationStateAsync())
            .ReturnsAsync(authState);

        Services.AddScoped<AuthenticationStateProvider>(_ => authProviderMock.Object);
    }

    // -------------------------------------------------------------------------
    // OnLocationChanged – nawigacja po renderze aktualizuje klasę active
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OnLocationChanged_AfterNavigation_UpdatesActiveClass()
    {
        // Arrange – tablica o ID=7, user ID=1
        var db = CreateDb();
        db.Tablice.Add(new Tablica
        {
            IdTablicy          = 7,
            NazwaTablicy       = "Navigation Board",
            IdUzytkownikaOwner = 1,
            TabliceUzyt        = new List<TablicaUzytkownik>()
        });
        await db.SaveChangesAsync();

        SetupAuth(1);

        // Startujemy na stronie głównej – link /board/7 nie jest aktywny
        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/");

        var cut = Render<NavMenu>();

        // Poczekaj, aż tablica się załaduje (DOM zawiera link)
        cut.WaitForAssertion(() =>
            Assert.NotNull(cut.Find("a[href='/board/7']")));

        // Link nie powinien być jeszcze aktywny
        var linkBefore = cut.Find("a[href='/board/7']");
        Assert.DoesNotContain("active", linkBefore.ClassName ?? "");

        // Act – nawigacja PO renderze: wyzwala OnLocationChanged
        nav.NavigateTo("/board/7");

        // Assert – po przerenderowaniu link ma klasę active
        cut.WaitForAssertion(() =>
            Assert.Contains("active",
                cut.Find("a[href='/board/7']").ClassName ?? ""));
    }

    // -------------------------------------------------------------------------
    // LoadBoardsAsync – fallback po Email: tablica wyświetlona po Email claim
    // -------------------------------------------------------------------------

    [Fact]
    public async Task LoadBoardsAsync_WithEmailClaimFallback_LoadsBoardsSuccessfully()
    {
        // Arrange – tworzymy użytkownika w DB i przypisaną do niego tablicę
        const string userEmail = "fallback@example.com";

        var db = CreateDb();

        var dbUser = new Uzytkownik
        {
            IdUzytkownika   = 42,
            Email           = userEmail,
            NazwaUzytkownika = "FallbackUser",
        };
        db.Uzytkownicy.Add(dbUser);

        db.Tablice.Add(new Tablica
        {
            IdTablicy          = 20,
            NazwaTablicy       = "Email Fallback Board",
            IdUzytkownikaOwner = 42,
            TabliceUzyt        = new List<TablicaUzytkownik>()
        });
        await db.SaveChangesAsync();

        // Auth: NameIdentifier = "not-an-int" → int.TryParse zwraca false
        //       Email = userEmail → ścieżka fallback w LoadBoardsAsync
        SetupAuthWithEmail(userEmail);

        // Act
        var cut = Render<NavMenu>();

        // Assert – tablica przypisana przez Email powinna być widoczna
        cut.WaitForAssertion(() =>
            Assert.Contains("Email Fallback Board", cut.Markup));
    }
}