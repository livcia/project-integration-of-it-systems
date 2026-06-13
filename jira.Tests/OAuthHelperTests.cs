using System.Security.Claims;
using FluentAssertions;
using jira.DbModels;
using Microsoft.AspNetCore.Authentication.Cookies;
using Xunit;

namespace jira.Tests;

public class OAuthHelperTests
{
    [Fact]
    public void BuildPrincipalShouldReturnPrincipalWithRequiredClaimsAndCorrectSchemeWhenUserHasMinimalData()
    {
        var user = new Uzytkownik
        {
            IdUzytkownika = 123,
            Email = "test@example.com",
            NazwaUzytkownika = "testuser",
            AvatarUrl = null,
            GoogleId = "",      
            GitHubId = null     
        };

        var result = OAuthHelper.BuildPrincipal(user);

        result.Should().NotBeNull();
        result.Identity.Should().NotBeNull();
        result.Identity!.AuthenticationType.Should().Be(CookieAuthenticationDefaults.AuthenticationScheme);
        result.Identity.IsAuthenticated.Should().BeTrue();

        result.FindFirst(ClaimTypes.NameIdentifier)?.Value.Should().Be("123");
        result.FindFirst(ClaimTypes.Email)?.Value.Should().Be("test@example.com");
        result.FindFirst(ClaimTypes.Name)?.Value.Should().Be("testuser");

        result.HasClaim(c => c.Type == "avatar_url").Should().BeFalse();
        result.HasClaim(c => c.Type == "google_id").Should().BeFalse();
        result.HasClaim(c => c.Type == "github_id").Should().BeFalse();
    }

    [Theory]
    [InlineData("https://avatar.com/image.png", "google-123", "github-456")]
    [InlineData("https://avatar.com/image.png", null, null)]
    [InlineData(null, "google-123", null)]
    [InlineData(null, null, "github-456")]
    public void BuildPrincipalShouldIncludeOptionalClaimsDependingOnUserProperties(string? avatarUrl, string? googleId, string? githubId)
    {
        var user = new Uzytkownik
        {
            IdUzytkownika = 1,
            Email = "user@domain.com",
            NazwaUzytkownika = "username",
            AvatarUrl = avatarUrl,
            GoogleId = googleId,
            GitHubId = githubId
        };

        var result = OAuthHelper.BuildPrincipal(user);

        if (!string.IsNullOrEmpty(avatarUrl))
        {
            result.FindFirst("avatar_url")?.Value.Should().Be(avatarUrl);
        }
        else
        {
            result.HasClaim(c => c.Type == "avatar_url").Should().BeFalse();
        }

        if (!string.IsNullOrEmpty(googleId))
        {
            result.FindFirst("google_id")?.Value.Should().Be(googleId);
        }
        else
        {
            result.HasClaim(c => c.Type == "google_id").Should().BeFalse();
        }

        if (!string.IsNullOrEmpty(githubId))
        {
            result.FindFirst("github_id")?.Value.Should().Be(githubId);
        }
        else
        {
            result.HasClaim(c => c.Type == "github_id").Should().BeFalse();
        }
    }
}