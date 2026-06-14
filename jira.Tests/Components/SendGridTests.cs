using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using jira.Services;

namespace jira.Tests.Services;

public class SendGridTests
{
    private readonly Mock<ILogger<SendGridEmailService>> _loggerMock = new();
    private readonly SendGridEmailService _service;

    public SendGridTests()
    {
        _service = new SendGridEmailService(_loggerMock.Object);
    }

    [Fact]
    public async Task SendAssignmentNotificationAsync_LogsWarning_WhenApiKeyIsMissing()
    {
        Environment.SetEnvironmentVariable("SENDGRID_API_KEY", null);

        await _service.SendAssignmentNotificationAsync("test@test.pl", "Jan", "Tytuł", "Tablica", 1, "Opis");

        VerifyLog(_loggerMock, LogLevel.Warning, "SENDGRID_API_KEY is not configured");
    }

    [Fact]
    public async Task SendAssignmentNotificationAsync_LogsError_WhenApiReturnsUnauthorized()
    {
        // Ustawiamy niepoprawny klucz, co wymusi odpowiedź 401 Unauthorized z serwera SendGrid
        Environment.SetEnvironmentVariable("SENDGRID_API_KEY", "invalid_key");
        Environment.SetEnvironmentVariable("SENDGRID_FROM_EMAIL", "test@test.pl");

        await _service.SendAssignmentNotificationAsync("a@a.pl", "U", "T", "B", 1, "D");

        // Weryfikujemy log error, który pojawia się przy kodzie statusu >= 400
        VerifyLog(_loggerMock, LogLevel.Error, "SendGrid returned error status");
    }

    // Pomocnicza metoda, która jest bardziej elastyczna dla logów ILogger
    private static void VerifyLog<T>(Mock<ILogger<T>> logger, LogLevel level, string messagePart)
    {
        logger.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(messagePart)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()
            ),
            Times.Once
        );
    }
}