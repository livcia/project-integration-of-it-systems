using Xunit;
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
}
