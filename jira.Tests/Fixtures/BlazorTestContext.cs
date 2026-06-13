using jira.Data;
using jira.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace jira.Tests.Fixtures;

/// <summary>
/// Przygotowanie bazy danych do testów z AppDbContext
/// </summary>
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
        // Cleanup if needed
    }
}

/// <summary>
/// Mock authentication state provider
/// </summary>
public class MockAuthenticationStateProvider : AuthenticationStateProvider
{
    private ClaimsPrincipal _currentPrincipal = new(new ClaimsIdentity());

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        return Task.FromResult(new AuthenticationState(_currentPrincipal));
    }

    public void SetAuthenticatedUser(int userId, string email, string username)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Name, username)
        };

        var identity = new ClaimsIdentity(claims, "test-auth");
        _currentPrincipal = new ClaimsPrincipal(identity);
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public void SetAnonymousUser()
    {
        _currentPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}

/// <summary>
/// Mock email service
/// </summary>
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

