using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using jira.DbModels;

namespace jira;

public static class OAuthHelper
{
    public static ClaimsPrincipal BuildPrincipal(Uzytkownik user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.IdUzytkownika.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.NazwaUzytkownika),
        };

        if (!string.IsNullOrEmpty(user.AvatarUrl))
            claims.Add(new Claim("avatar_url", user.AvatarUrl));

        if (!string.IsNullOrEmpty(user.GoogleId))
            claims.Add(new Claim("google_id", user.GoogleId));

        if (!string.IsNullOrEmpty(user.GitHubId))
            claims.Add(new Claim("github_id", user.GitHubId));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }
}