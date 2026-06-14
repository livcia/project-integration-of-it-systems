using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using jira.Services;
using jira.Tests.Fixtures;

namespace jira.Tests.Services;

/// <summary>
/// Testy jednostkowe dla IEmailService przy użyciu MockEmailService
/// z BlazorTestContext.
/// </summary>
public class SendGridEmailServiceTests
{
    // -----------------------------------------------------------------------
    // SendAssignmentNotificationAsync – email zapisywany do SentEmails
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SendAssignmentNotificationAsync_AddsEntryToSentEmails()
    {
        // Arrange
        var mockEmailService = new MockEmailService();

        // Act
        await mockEmailService.SendAssignmentNotificationAsync(
            toEmail: "user@example.com",
            toName: "Jan Kowalski",
            taskTitle: "Naprawić błąd logowania",
            boardName: "Sprint 1",
            taskId: 7,
            taskDescription: "Opis zadania");

        // Assert – email został zapisany
        Assert.Single(mockEmailService.SentEmails);
    }

    // -----------------------------------------------------------------------
    // Po wywołaniu SentEmails.Count == 1
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SendAssignmentNotificationAsync_CalledOnce_SentEmailsCountIsOne()
    {
        // Arrange
        var mockEmailService = new MockEmailService();

        // Act
        await mockEmailService.SendAssignmentNotificationAsync(
            toEmail: "alice@example.com",
            toName: "Alice",
            taskTitle: "Zadanie testowe",
            boardName: "Tablica A",
            taskId: 1,
            taskDescription: null);

        // Assert
        Assert.Single(mockEmailService.SentEmails);
    }

    // -----------------------------------------------------------------------
    // Format wpisu to "email|taskTitle"
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SendAssignmentNotificationAsync_EntryFormatIsEmailPipeTaskTitle()
    {
        // Arrange
        var mockEmailService = new MockEmailService();
        const string email = "bob@example.com";
        const string taskTitle = "Wdrożyć CI/CD";

        // Act
        await mockEmailService.SendAssignmentNotificationAsync(
            toEmail: email,
            toName: "Bob",
            taskTitle: taskTitle,
            boardName: "DevOps Board",
            taskId: 42,
            taskDescription: "Skonfigurować pipeline");

        // Assert – format wpisu: "email|taskTitle"
        var entry = Assert.Single(mockEmailService.SentEmails);
        Assert.Equal($"{email}|{taskTitle}", entry);
    }

    // -----------------------------------------------------------------------
    // Pomocnik do weryfikacji logów przez Moq
    // -----------------------------------------------------------------------

    private static void VerifyLog<T>(
        Mock<ILogger<T>> loggerMock,
        LogLevel expectedLevel,
        Times times)
    {
        loggerMock.Verify(
            x => x.Log(
                expectedLevel,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);
    }

    // -----------------------------------------------------------------------
    // SendAssignmentNotificationAsync – brak SENDGRID_FROM_EMAIL → LogWarning
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SendAssignmentNotificationAsync_LogsWarning_WhenFromEmailIsMissing()
    {
        // Arrange – upewniamy się, że SENDGRID_API_KEY jest ustawiony (żeby
        // dotrzeć do sprawdzenia FROM_EMAIL), ale FROM_EMAIL jest pusty.
        Environment.SetEnvironmentVariable("SENDGRID_API_KEY", "test-api-key");
        Environment.SetEnvironmentVariable("SENDGRID_FROM_EMAIL", null);

        var loggerMock = new Mock<ILogger<SendGridEmailService>>();
        var service = new SendGridEmailService(loggerMock.Object);

        // Act – metoda nie powinna rzucić wyjątku
        var exception = await Record.ExceptionAsync(() =>
            service.SendAssignmentNotificationAsync(
                toEmail: "recipient@example.com",
                toName: "Test User",
                taskTitle: "Zadanie bez FROM_EMAIL",
                boardName: "Board X",
                taskId: 99,
                taskDescription: null));

        // Assert – graceful handling (brak wyjątku)
        Assert.Null(exception);

        // Assert – zalogowano ostrzeżenie o brakującej konfiguracji
        VerifyLog(loggerMock, LogLevel.Warning, Times.Once());

        // Cleanup
        Environment.SetEnvironmentVariable("SENDGRID_API_KEY", null);
    }

    // -----------------------------------------------------------------------
    // SendAssignmentNotificationAsync – brak SENDGRID_API_KEY → LogWarning
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SendAssignmentNotificationAsync_LogsWarning_WhenApiKeyIsMissing()
    {
        // Arrange – API_KEY brakuje, FROM_EMAIL może być dowolny.
        Environment.SetEnvironmentVariable("SENDGRID_API_KEY", null);

        var loggerMock = new Mock<ILogger<SendGridEmailService>>();
        var service = new SendGridEmailService(loggerMock.Object);

        // Act – metoda nie powinna rzucić wyjątku
        var exception = await Record.ExceptionAsync(() =>
            service.SendAssignmentNotificationAsync(
                toEmail: "recipient@example.com",
                toName: "Test User",
                taskTitle: "Zadanie bez API_KEY",
                boardName: "Board Y",
                taskId: 1,
                taskDescription: null));

        // Assert – graceful handling (brak wyjątku)
        Assert.Null(exception);

        // Assert – zalogowano ostrzeżenie o brakującym kluczu API
        VerifyLog(loggerMock, LogLevel.Warning, Times.Once());
    }

    // -----------------------------------------------------------------------
    // SendAssignmentNotificationAsync – wyjątek sieciowy → blok catch → LogError
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SendAssignmentNotificationAsync_LogsError_WhenExceptionIsThrown()
    {
        // Arrange – ustawiamy zmienne środowiskowe tak, żeby serwis dotarł
        // do wywołania SendGrid, ale podajemy nieprawidłowy klucz API, który
        // spowoduje że konstruktor SendGridClient rzuci wyjątek podczas próby
        // wysłania (lub zadamy invalid host, żeby HttpClient rzucił wyjątek).
        // Najprostsze podejście: ustawiamy klucz i FROM_EMAIL, ale FROM_EMAIL
        // zawiera znaki powodujące błąd EmailAddress – w praktyce podajemy
        // poprawne wartości i wymuszamy wyjątek przez null w toEmail.
        Environment.SetEnvironmentVariable("SENDGRID_API_KEY", "SG.fake-key-that-will-cause-http-error");
        Environment.SetEnvironmentVariable("SENDGRID_FROM_EMAIL", "from@example.com");
        Environment.SetEnvironmentVariable("SENDGRID_FROM_NAME", "System Test");

        var loggerMock = new Mock<ILogger<SendGridEmailService>>();
        var service = new SendGridEmailService(loggerMock.Object);

        // Act – null jako toEmail spowoduje ArgumentNullException wewnątrz
        // SendGrid MailHelper.CreateSingleEmail → zostanie złapany przez catch
        var exception = await Record.ExceptionAsync(() =>
            service.SendAssignmentNotificationAsync(
                toEmail: null!,
                toName: "Test User",
                taskTitle: "Zadanie z wyjątkiem",
                boardName: "Board Z",
                taskId: 55,
                taskDescription: null));

        // Assert – blok catch połknął wyjątek (graceful handling)
        Assert.Null(exception);

        // Assert – blok catch zalogował błąd z poziomem Error
        VerifyLog(loggerMock, LogLevel.Error, Times.Once());

        // Cleanup
        Environment.SetEnvironmentVariable("SENDGRID_API_KEY", null);
        Environment.SetEnvironmentVariable("SENDGRID_FROM_EMAIL", null);
        Environment.SetEnvironmentVariable("SENDGRID_FROM_NAME", null);
    }
}
