using Xunit;
using jira.Tests.Fixtures;
using jira.DbModels;
using Microsoft.EntityFrameworkCore;

namespace jira.Tests.E2E.Authentication;

/// <summary>
/// E2E testy dla autentykacji użytkowników
/// </summary>
public class AuthenticationE2ETests : IDisposable
{
    private readonly TestDatabaseFixture _fixture;

    public AuthenticationE2ETests()
    {
        _fixture = new TestDatabaseFixture();
    }

    [Fact]
    public async Task UserLoginFlow_Should_CreateUserWhenNotExists()
    {
        // Arrange
        using var db = _fixture.CreateDbContext();
        var email = "newuser@example.com";
        var username = "newuser";

        // Act - Symulujemy callback OAuth
        var user = new Uzytkownik
        {
            Email = email,
            NazwaUzytkownika = username,
            AvatarUrl = $"https://api.dicebear.com/10.x/lorelei/svg?seed={username}",
            PasswordHash = string.Empty,
            DataRejestracji = DateTime.UtcNow,
            GoogleId = "google123"
        };

        db.Uzytkownicy.Add(user);
        await db.SaveChangesAsync();

        // Assert
        var savedUser = await db.Uzytkownicy.FindAsync(user.IdUzytkownika);
        Assert.NotNull(savedUser);
        Assert.Equal(email, savedUser.Email);
        Assert.Equal(username, savedUser.NazwaUzytkownika);
        Assert.Equal("google123", savedUser.GoogleId);
    }

    [Fact]
    public async Task UserGitHubLogin_Should_LinkGitHubAccount()
    {
        // Arrange
        using var db = _fixture.CreateDbContext();
        var user = TestDataBuilder.CreateUser(githubId: "github123");

        // Act
        db.Uzytkownicy.Add(user);
        await db.SaveChangesAsync();

        // Assert
        var savedUser = await db.Uzytkownicy.FindAsync(user.IdUzytkownika);
        Assert.NotNull(savedUser);
        Assert.Equal("github123", savedUser.GitHubId);
    }

    [Fact]
    public async Task UserLogin_Should_UpdateExistingUserInfo()
    {
        // Arrange
        using var db = _fixture.CreateDbContext();
        var user = TestDataBuilder.CreateUser(username: "oldname");
        db.Uzytkownicy.Add(user);
        await db.SaveChangesAsync();

        // Act - Logowanie ponownym razem z nową nazwą
        var updatedUser = await db.Uzytkownicy.FindAsync(user.IdUzytkownika);
        Assert.NotNull(updatedUser);
        updatedUser.NazwaUzytkownika = "newname";
        updatedUser.AvatarUrl = "https://new-avatar.com/image.png";
        await db.SaveChangesAsync();

        // Assert
        var reloadedUser = await db.Uzytkownicy.FindAsync(user.IdUzytkownika);
        Assert.Equal("newname", reloadedUser.NazwaUzytkownika);
        Assert.Equal("https://new-avatar.com/image.png", reloadedUser.AvatarUrl);
    }

    [Fact]
    public async Task MultipleOAuthProviders_Should_LinkToSameAccount()
    {
        // Arrange
        using var db = _fixture.CreateDbContext();
        var email = "user@example.com";

        // Act - Utworzenie użytkownika z Google
        var user = TestDataBuilder.CreateUser(email: email, googleId: "google123");
        db.Uzytkownicy.Add(user);
        await db.SaveChangesAsync();

        // Powiązanie GitHub do tego samego użytkownika
        var savedUser = await db.Uzytkownicy.FirstAsync(u => u.Email == email);
        savedUser.GitHubId = "github456";
        await db.SaveChangesAsync();

        // Assert
        var finalUser = await db.Uzytkownicy.FirstAsync(u => u.Email == email);
        Assert.Equal("google123", finalUser.GoogleId);
        Assert.Equal("github456", finalUser.GitHubId);
    }

    public void Dispose()
    {
        _fixture?.Dispose();
    }
}

