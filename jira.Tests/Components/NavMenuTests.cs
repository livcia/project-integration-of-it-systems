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

    private AppDbContext CreateDb()
    {
        var dbName = Guid.NewGuid().ToString();
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var db = new AppDbContext(opts);

        Services.AddSingleton<AppDbContext>(db);

        return db;
    }

    [Fact]
    public void NavMenu_WhenNoBoards_ShowsEmptyMessage()
    {
        CreateDb();
        SetupAuth(1);

        var cut = Render<NavMenu>();

        cut.WaitForAssertion(() =>
            Assert.Contains("Brak tablic", cut.Markup));
    }

    [Fact]
    public async Task NavMenu_OnActiveLink_AppliesActiveClass()
    {
        var db = CreateDb();
        db.Tablice.Add(new Tablica
        {
            IdTablicy = 5,
            NazwaTablicy = "Active Board",
            IdUzytkownikaOwner = 1,
            TabliceUzyt = new List<TablicaUzytkownik>()
        });
        await db.SaveChangesAsync();

        SetupAuth(1);

        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/board/5");

        var cut = Render<NavMenu>();

        cut.WaitForAssertion(() =>
            Assert.Contains("active",
                cut.Find("a[href='/board/5']").ClassName ?? ""));
    }

    [Fact]
    public async Task NavMenu_RefreshesList_WhenBoardStateServiceNotifies()
    {
        var db = CreateDb();
        SetupAuth(1);

        var boardState = new BoardStateService();
        Services.AddSingleton(boardState);

        var cut = Render<NavMenu>();

        Assert.Contains("Brak tablic", cut.Markup);

        db.Tablice.Add(new Tablica { IdTablicy = 10, NazwaTablicy = "Nowa Tablica", IdUzytkownikaOwner = 1 });
        await db.SaveChangesAsync();

        await boardState.NotifyBoardCreatedAsync();

        cut.WaitForState(() => cut.Markup.Contains("Nowa Tablica"));
        Assert.DoesNotContain("Brak tablic", cut.Markup);
    }

    [Fact]
    public async Task NavMenu_OnlyShowsUserOwnBoards()
    {
        var db = CreateDb();
        db.Tablice.Add(new Tablica { IdTablicy = 1, NazwaTablicy = "Moja", IdUzytkownikaOwner = 1 });
        db.Tablice.Add(new Tablica { IdTablicy = 2, NazwaTablicy = "Obca", IdUzytkownikaOwner = 2 });
        await db.SaveChangesAsync();

        SetupAuth(1);

        var cut = Render<NavMenu>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Moja", cut.Markup);
            Assert.DoesNotContain("Obca", cut.Markup);
        });
    }
}