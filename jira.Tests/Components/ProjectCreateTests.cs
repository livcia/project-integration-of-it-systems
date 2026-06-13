using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Bunit;
using jira.Data;
using jira.DbModels;
using jira.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using jira.Components.Pages.ProjectCreate;

namespace jira.Tests.Components.Pages;

/// <summary>
/// Testy bUnit dla komponentu tworzenia nowej tablicy (strona /projects/new).
/// Weryfikują poprawność struktury DOM, walidację formularza, interakcje z paletą kolorów
/// oraz obsługę błędów dla nieuwierzytelnionych użytkowników.
/// </summary>
public class ProjectCreateTests : BunitContext
{
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<BoardStateService> _boardStateMock;
    private readonly AppDbContext _dbContext;
    private readonly FakeAuthenticationStateProvider _authStateProvider;

    public ProjectCreateTests()
    {
        // 1. Konfiguracja odizolowanej bazy danych InMemory
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"JiraProjectCreateDb_{Guid.NewGuid()}")
            .Options;
        
        _dbContext = new AppDbContext(options);

        // 2. Mockowanie hierarchii IServiceScopeFactory na potrzeby wstrzykiwania bazy przez ScopeFactory
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        var scopeMock = new Mock<IServiceScope>();

        _scopeFactoryMock.Setup(x => x.CreateScope()).Returns(scopeMock.Object);
        scopeMock.Setup(x => x.ServiceProvider).Returns(_serviceProviderMock.Object);
        _serviceProviderMock.Setup(x => x.GetService(typeof(AppDbContext))).Returns(_dbContext);

        // 3. Mockowanie serwisu stanu tablic
        _boardStateMock = new Mock<BoardStateService>();

        // 4. Konfiguracja autentykacji
        _authStateProvider = new FakeAuthenticationStateProvider();

        // 5. Rejestracja usług w kontenerze DI środowiska bUnit
        Services.AddSingleton(_scopeFactoryMock.Object);
        Services.AddSingleton(_boardStateMock.Object);
        Services.AddSingleton<AuthenticationStateProvider>(_authStateProvider);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private void SetupUser(int userId, string email, string name, bool isAuthenticated = true)
    {
        if (!isAuthenticated)
        {
            _authStateProvider.SetAuthenticationState(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
            return;
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, name),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Email, email)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        
        _authStateProvider.SetAuthenticationState(new AuthenticationState(principal));
    }

    // -----------------------------------------------------------------------
    // Testy Renderowania i Struktury DOM
    // -----------------------------------------------------------------------

    [Fact]
    public void Render_InitialState_DisplaysCorrectHeadersAndDefaultColor()
    {
        // Arrange
        SetupUser(1, "test@jira.pl", "User Test");

        // Act
        var cut = Render<ProjectCreate>();

        // Assert
        Assert.Equal("Nowa Tablica", cut.Find("h1.create-board-title").TextContent);
        
        var selectedColorButton = cut.Find("button.cb-color-swatch--selected");
        Assert.Equal("#0052CC", selectedColorButton.GetAttribute("title"));
        
        var previewText = cut.Find(".cb-color-preview-text strong");
        Assert.Equal("#0052CC", previewText.TextContent);
    }

    // -----------------------------------------------------------------------
    // Testy Interakcji Interfejsu (UI Interactions)
    // -----------------------------------------------------------------------

    [Fact]
    public void ColorPicker_ClickingSwatch_ChangesSelectedColorAndPreview()
    {
        // Arrange
        SetupUser(1, "test@jira.pl", "User Test");
        var cut = Render<ProjectCreate>();
        
        const string targetColor = "#FF5630";

        // Act
        var colorButton = cut.Find($"button[title='{targetColor}']");
        colorButton.Click();

        // Assert
        Assert.Contains("cb-color-swatch--selected", colorButton.ClassName);
        
        var previewText = cut.Find(".cb-color-preview-text strong");
        Assert.Equal(targetColor, previewText.TextContent);
    }

    // -----------------------------------------------------------------------
    // Testy Walidacji Formularza (Validation)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Submit_EmptyBoardName_TriggersClientSideValidationError()
    {
        // Arrange
        SetupUser(1, "test@jira.pl", "User Test");
        var cut = Render<ProjectCreate>();

        // Act
        var form = cut.Find("form");
        await form.SubmitAsync();

        // Assert
        var errorSpan = cut.Find(".cb-field-error");
        Assert.Equal("Nazwa tablicy jest wymagana.", errorSpan.TextContent.Trim());
        
        Assert.False(await _dbContext.Tablice.AnyAsync());
    }

    [Fact]
    public async Task Submit_NameTooShort_TriggersLengthValidationError()
    {
        // Arrange
        SetupUser(1, "test@jira.pl", "User Test");
        var cut = Render<ProjectCreate>();

        // Act
        cut.Find("#board-name").Change("X");
        var form = cut.Find("form");
        await form.SubmitAsync();

        // Assert
        var errorSpan = cut.Find(".cb-field-error");
        Assert.Equal("Nazwa musi mieć od 2 do 120 znaków.", errorSpan.TextContent.Trim());
    }

    // -----------------------------------------------------------------------
    // Testy Logiki Biznesowej (Backend & Integration Errors)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Submit_UnauthenticatedUser_RendersConditionalErrorAlert()
    {
        // Arrange
        SetupUser(0, "", "", isAuthenticated: false);
        var cut = Render<ProjectCreate>();

        // Act
        cut.Find("#board-name").Change("Projekt Bez Autoryzacji");
        var form = cut.Find("form");
        await form.SubmitAsync();

        // Assert
        var errorAlert = cut.Find("div.cb-alert-error");
        Assert.Contains("Musisz być zalogowany, aby stworzyć tablicę.", errorAlert.TextContent);
    }

    protected override void Dispose(bool disposing)
    {
        _dbContext.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>
/// Lekki, lokalny Provider Stanu Autoryzacji imitujący zachowanie zalogowanej sesji w Blazor.
/// </summary>
public class FakeAuthenticationStateProvider : AuthenticationStateProvider
{
    private Task<AuthenticationState> _authenticationStateTask = 
        Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => _authenticationStateTask;

    public void SetAuthenticationState(AuthenticationState state)
    {
        _authenticationStateTask = Task.FromResult(state);
        NotifyAuthenticationStateChanged(_authenticationStateTask);
    }
}