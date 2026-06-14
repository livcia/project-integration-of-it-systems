using jira.Data;
using jira.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace jira.Tests.Fixtures;

public class TestDatabaseFixture : IDisposable
{
    private DbContextOptions<AppDbContext>? _dbContextOptions;

    public TestDatabaseFixture()
    {
        _dbContextOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    public AppDbContext CreateDbContext()
    {
        return new AppDbContext(_dbContextOptions);
    }

    public void Dispose()
    {
    }
}

public class MockEmailService : IEmailService
{
    public List<string> SentEmails { get; } = new();

    public Task SendAssignmentNotificationAsync(
        string toEmail,
        string toName,
        string taskTitle,
        string boardName,
        int taskId,
        string? taskDescription)
    {
        SentEmails.Add($"{toEmail}|{taskTitle}");
        return Task.CompletedTask;
    }
}

