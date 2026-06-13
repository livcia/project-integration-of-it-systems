using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Xunit;
using jira.DbModels;

namespace jira.Tests.Services;

/// <summary>
/// Testy jednostkowe dla OAuthHelper.BuildPrincipal().
/// </summary>
public class OAuthHelperTests
{
    // Helper tworzący minimalny obiekt Uzytkownik z wymaganymi polami
    private static Uzytkownik CreateUser(
        int id = 1,
        string email = "test@example.com",
        string username = "testuser",
        string? avatarUrl = null,
        string? googleId = null,
        string? gitHubId = null)
    {
        return new Uzytkownik
        {
            IdUzytkownika = id,
            Email = email,
            NazwaUzytkownika = username,
            AvatarUrl = avatarUrl,
            GoogleId = googleId,
            GitHubId = gitHubId
        };
    }

    // -----------------------------------------------------------------------
    // Claim "google_id" – użytkownik z GoogleId
    // -----------------------------------------------------------------------

    [Fact]
    public void BuildPrincipal_UserWithGoogleId_HasGoogleIdClaim()
    {
        // Arrange
        var user = CreateUser(googleId: "google-abc-123");

        // Act
        var principal = OAuthHelper.BuildPrincipal(user);

        // Assert
        var claim = principal.FindFirst("google_id");
        Assert.NotNull(claim);
        Assert.Equal("google-abc-123", claim.Value);
    }

    // -----------------------------------------------------------------------
    // Claim "github_id" – użytkownik z GitHubId
    // -----------------------------------------------------------------------

    [Fact]
    public void BuildPrincipal_UserWithGitHubId_HasGitHubIdClaim()
    {
        // Arrange
        var user = CreateUser(gitHubId: "github-xyz-456");

        // Act
        var principal = OAuthHelper.BuildPrincipal(user);

        // Assert
        var claim = principal.FindFirst("github_id");
        Assert.NotNull(claim);
        Assert.Equal("github-xyz-456", claim.Value);
    }

    // -----------------------------------------------------------------------
    // Brak claima "avatar_url" gdy AvatarUrl jest null
    // -----------------------------------------------------------------------

    [Fact]
    public void BuildPrincipal_UserWithoutAvatarUrl_DoesNotHaveAvatarUrlClaim()
    {
        // Arrange
        var user = CreateUser(avatarUrl: null);

        // Act
        var principal = OAuthHelper.BuildPrincipal(user);

        // Assert
        var claim = principal.FindFirst("avatar_url");
        Assert.Null(claim);
    }

    // -----------------------------------------------------------------------
    // Użytkownik z pełnymi danymi – wszystkie claime obecne
    // -----------------------------------------------------------------------

    [Fact]
    public void BuildPrincipal_UserWithAllData_HasAllRequiredClaims()
    {
        // Arrange
        var user = CreateUser(
            id: 42,
            email: "full@example.com",
            username: "fulluser",
            avatarUrl: "https://example.com/avatar.png",
            googleId: "g-id-001",
            gitHubId: "gh-id-002");

        // Act
        var principal = OAuthHelper.BuildPrincipal(user);

        // Assert – claime obowiązkowe
        Assert.Equal("42", principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("full@example.com", principal.FindFirstValue(ClaimTypes.Email));
        Assert.Equal("fulluser", principal.FindFirstValue(ClaimTypes.Name));

        // Assert – claime opcjonalne
        Assert.Equal("https://example.com/avatar.png", principal.FindFirstValue("avatar_url"));
        Assert.Equal("g-id-001", principal.FindFirstValue("google_id"));
        Assert.Equal("gh-id-002", principal.FindFirstValue("github_id"));
    }

    // -----------------------------------------------------------------------
    // Scheme uwierzytelnienia to CookieAuthenticationDefaults.AuthenticationScheme
    // -----------------------------------------------------------------------

    [Fact]
    public void BuildPrincipal_ReturnsIdentityWithCookieAuthenticationScheme()
    {
        // Arrange
        var user = CreateUser();

        // Act
        var principal = OAuthHelper.BuildPrincipal(user);

        // Assert
        var identity = principal.Identity as ClaimsIdentity;
        Assert.NotNull(identity);
        Assert.Equal(CookieAuthenticationDefaults.AuthenticationScheme, identity.AuthenticationType);
    }
}
