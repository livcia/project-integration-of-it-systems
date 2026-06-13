using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Bunit;
using jira.Data;
using jira.DbModels;
using jira.Services;
using jira.Tests;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using jira.Components.Pages.TicketCreate;

namespace jira.Tests.Components.Pages;

public class TicketCreateTests : BunitContext
{
    private readonly AppDbContext _dbContext;
    private readonly FakeAuthenticationStateProvider _authStateProvider;
    private readonly Mock<IEmailService> _emailServiceMock;

    public TicketCreateTests()
    {
        // 1. Czysta baza InMemory z unikalną nazwą dla każdego przebiegu testu
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"JiraTicketSimpleDb_{Guid.NewGuid()}")
            .Options;
        
        _dbContext = new AppDbContext(options);

        // 2. Mock zewnętrznej usługi e-mail
        _emailServiceMock = new Mock<IEmailService>();

        // 3. Rejestracja usług wprost do bUnit
        Services.AddScoped(_ => _dbContext);
        Services.AddSingleton(_emailServiceMock.Object);
        
        _authStateProvider = new FakeAuthenticationStateProvider();
        Services.AddSingleton<AuthenticationStateProvider>(_authStateProvider);
    }

    private void SetupUser(int userId, string email, string name)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, name),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Email, email)
        };
        _authStateProvider.SetAuthenticationState(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))));
    }

    private async Task SeedTestDataAsync(int boardId, string boardName, int ownerId, int memberId)
    {
        var owner = new Uzytkownik { IdUzytkownika = ownerId, Email = "owner@jira.pl", NazwaUzytkownika = "Projekt Manager" };
        var member = new Uzytkownik { IdUzytkownika = memberId, Email = "dev@jira.pl", NazwaUzytkownika = "Starszy Programista" };

        var board = new Tablica
        {
            IdTablicy = boardId,
            NazwaTablicy = boardName,
            IdUzytkownikaOwner = ownerId,
            Owner = owner
        };

        var boardMember = new TablicaUzytkownik
        {
            IdTablicy = boardId,
            IdUzytkownika = memberId,
            Uzytkownik = member
        };

        _dbContext.Uzytkownicy.AddRange(owner, member);
        _dbContext.Tablice.Add(board);
        _dbContext.Set<TablicaUzytkownik>().Add(boardMember);
        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public void Render_MissingBoardIdQueryParam_DisplaysErrorAlert()
    {
        SetupUser(1, "user@jira.pl", "Test User");

        // Pozostawiamy URL bez parametrów query
        var cut = Render<TicketCreate>();

        var errorAlert = cut.Find("div.cb-alert-error");
        Assert.Contains("Brak identyfikatora tablicy", errorAlert.TextContent);
        Assert.Empty(cut.FindAll("form"));
    }

    [Fact]
    public async Task Render_WithValidBoardId_LoadsBoardNameAndDropdownUsers()
    {
        const int testBoardId = 15;
        SetupUser(10, "pm@jira.pl", "Manager");
        await SeedTestDataAsync(testBoardId, "Projekt Alfa", ownerId: 10, memberId: 20);

        // Symulacja Query Stringa poprzez Navigation Manager przed wyrenderowaniem komponentu
        var navManager = Services.GetRequiredService<NavigationManager>();
        navManager.NavigateTo($"http://localhost/ticket/new?boardId={testBoardId}");

        var cut = Render<TicketCreate>();

        // Czekamy na zakończenie ładowania danych asynchronicznych (aż zniknie spinner .cb-loading)
        cut.WaitForState(() => cut.FindAll(".cb-loading").Count == 0, TimeSpan.FromSeconds(3));

        // Sprawdzenie nazwy tablicy
        var subtitle = cut.Find(".create-board-subtitle strong");
        Assert.Equal("Projekt Alfa", subtitle.TextContent);

        // Sprawdzenie listy użytkowników
        var options = cut.FindAll("#ticket-assignee option");
        Assert.Equal(3, options.Count);
        Assert.Contains("— Nieprzypisane —", options[0].TextContent);
        Assert.Contains("Projekt Manager", options[1].TextContent);
        Assert.Contains("Starszy Programista", options[2].TextContent);
    }

    [Fact]
    public async Task Validation_OnBlur_ShowsRequiredErrorForTitle()
    {
        const int testBoardId = 15;
        SetupUser(10, "pm@jira.pl", "Manager");
        await SeedTestDataAsync(testBoardId, "Projekt Alfa", ownerId: 10, memberId: 20);
        
        var navManager = Services.GetRequiredService<NavigationManager>();
        navManager.NavigateTo($"http://localhost/ticket/new?boardId={testBoardId}");

        var cut = Render<TicketCreate>();

        // Czekamy na schowanie spinnera ładowania
        cut.WaitForState(() => cut.FindAll(".cb-loading").Count == 0, TimeSpan.FromSeconds(3));

        var inputTitle = cut.Find("#ticket-name");
        await inputTitle.BlurAsync();

        var errorSpan = cut.Find(".cb-field-error");
        Assert.Equal("Tytuł jest wymagany.", errorSpan.TextContent.Trim());
        Assert.Contains("cb-input--error", inputTitle.ClassName);
    }

    [Fact]
    public async Task Form_InitializesWithCorrectDefaultValues()
    {
        const int testBoardId = 15;
        SetupUser(10, "pm@jira.pl", "Manager");
        await SeedTestDataAsync(testBoardId, "Projekt Alfa", ownerId: 10, memberId: 20);

        var navManager = Services.GetRequiredService<NavigationManager>();
        navManager.NavigateTo($"http://localhost/ticket/new?boardId={testBoardId}");

        var cut = Render<TicketCreate>();

        // Czekamy na schowanie spinnera ładowania
        cut.WaitForState(() => cut.FindAll(".cb-loading").Count == 0, TimeSpan.FromSeconds(3));

        var prioritySelect = cut.Find("#ticket-priority");
        Assert.Equal("sredni", prioritySelect.GetAttribute("value"));

        var columnSelect = cut.Find("#ticket-column");
        Assert.Equal("Todo", columnSelect.GetAttribute("value"));
    }

    protected override void Dispose(bool disposing)
    {
        _dbContext.Dispose();
        base.Dispose(disposing);
    }
}