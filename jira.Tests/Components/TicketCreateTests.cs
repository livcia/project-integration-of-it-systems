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
    private readonly DbContextOptions<AppDbContext> _dbOptions;
    private readonly FakeAuthenticationStateProvider _authStateProvider;
    private readonly Mock<IEmailService> _emailServiceMock;

    public TicketCreateTests()
    {
        // 1. Definiujemy unikalną bazę danych w pamięci dla każdego przebiegu testowego
        _dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"JiraTicketSimpleDb_{Guid.NewGuid()}")
            .Options;

        // 2. Mock zewnętrznej usługi e-mail
        _emailServiceMock = new Mock<IEmailService>();

        // 3. Rejestracja usług – komponent dostanie świeżą instancję contextu na żądanie
        Services.AddScoped(sp => new AppDbContext(_dbOptions));
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
        // Otwieramy izolowany kontekst na czas przygotowania danych (Arrange)
        using var context = new AppDbContext(_dbOptions);

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

        context.Uzytkownicy.AddRange(owner, member);
        context.Tablice.Add(board);
        context.Set<TablicaUzytkownik>().Add(boardMember);
        await context.SaveChangesAsync();
    }

    [Fact]
    public void Render_MissingBoardIdQueryParam_DisplaysErrorAlert()
    {
        SetupUser(1, "user@jira.pl", "Test User");

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

        var navManager = Services.GetRequiredService<NavigationManager>();
        navManager.NavigateTo($"http://localhost/ticket/new?boardId={testBoardId}");

        var cut = Render<TicketCreate>();

        cut.WaitForState(() => cut.FindAll(".cb-loading").Count == 0, TimeSpan.FromSeconds(3));

        var subtitle = cut.Find(".create-board-subtitle strong");
        Assert.Equal("Projekt Alfa", subtitle.TextContent);

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

        cut.WaitForState(() => cut.FindAll(".cb-loading").Count == 0, TimeSpan.FromSeconds(3));

        var prioritySelect = cut.Find("#ticket-priority");
        Assert.Equal("sredni", prioritySelect.GetAttribute("value"));

        var columnSelect = cut.Find("#ticket-column");
        Assert.Equal("Todo", columnSelect.GetAttribute("value"));
    }

    [Fact]
    public async Task HandleSubmit_ValidData_SavesToDbSendsEmailAndRedirects()
    {
        const int testBoardId = 15;
        SetupUser(10, "pm@jira.pl", "Manager");
        await SeedTestDataAsync(testBoardId, "Projekt Alfa", ownerId: 10, memberId: 20);

        var navManager = Services.GetRequiredService<NavigationManager>();
        navManager.NavigateTo($"http://localhost/ticket/new?boardId={testBoardId}");

        var cut = Render<TicketCreate>();
        cut.WaitForState(() => cut.FindAll(".cb-loading").Count == 0, TimeSpan.FromSeconds(3));

        // Act – Wypełnianie pól formularza
        cut.Find("#ticket-name").Change("Naprawić błąd logowania");
        cut.Find("#ticket-desc").Change("Użytkownicy zgłaszają problem z logowaniem przez Google");
        cut.Find("#ticket-priority").Change("wysoki");
        cut.Find("#ticket-column").Change("In Progress");
        cut.Find("#ticket-assignee").Change("20");

        cut.Find("form").Submit();

        // Assert – Sprawdzenie bazy danych przy użyciu świeżego, bezpiecznego kontekstu
        using (var dbCheck = new AppDbContext(_dbOptions))
        {
            var savedTask = await dbCheck.Set<Zadanie>().FirstOrDefaultAsync(z => z.IdTablicy == testBoardId);
            Assert.NotNull(savedTask);
            Assert.Equal("Naprawić błąd logowania", savedTask!.TytulZadania);
            Assert.Equal("wysoki", savedTask.Priorytet);
            Assert.Equal("In Progress", savedTask.Status);
            Assert.Equal(20, savedTask.IdUzytkownikaPrzypisanego);
            Assert.Equal(10, savedTask.IdUzytkownikaTworcyZadania);
        }

        // Weryfikacja mocka usługi pocztowej
        _emailServiceMock.Verify(x => x.SendAssignmentNotificationAsync(
            "dev@jira.pl",
            "Starszy Programista",
            "Naprawić błąd logowania",
            "Projekt Alfa",
            It.IsAny<int>(),
            "Użytkownicy zgłaszają problem z logowaniem przez Google"
        ), Times.Once);

        await Task.Delay(1300);
        Assert.EndsWith($"/board/{testBoardId}", navManager.Uri);
    }

    [Fact]
    public async Task SubmittingForm_WithInvalidData_TriggersValidationAndDoesNotSave()
    {
        const int testBoardId = 15;
        SetupUser(10, "pm@jira.pl", "Manager");
        await SeedTestDataAsync(testBoardId, "Projekt Alfa", ownerId: 10, memberId: 20);

        var navManager = Services.GetRequiredService<NavigationManager>();
        navManager.NavigateTo($"http://localhost/ticket/new?boardId={testBoardId}");

        var cut = Render<TicketCreate>();
        cut.WaitForState(() => cut.FindAll(".cb-loading").Count == 0, TimeSpan.FromSeconds(3));

        cut.Find("form").Submit();

        var errorSpan = cut.Find(".cb-field-error");
        Assert.Equal("Tytuł jest wymagany.", errorSpan.TextContent.Trim());

        // Assert – Sprawdzenie licznika bazy danych za pomocą bezpiecznego kontekstu
        using (var dbCheck = new AppDbContext(_dbOptions))
        {
            var tasksInDb = await dbCheck.Set<Zadanie>().CountAsync();
            Assert.Equal(0, tasksInDb);
        }
    }

    // Stara metoda Dispose nie jest już wymagana, bUnit posprząta kontenery DI automatycznie
}