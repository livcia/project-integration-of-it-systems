using DotNetEnv;
using jira;
using jira.Components;
using jira.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var envPath = Path.Combine(Directory.GetCurrentDirectory(), "..", ".env");
if (File.Exists(envPath))
    Env.Load(envPath);

var envRoot = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (File.Exists(envRoot))
    Env.Load(envRoot);

var builder = WebApplication.CreateBuilder(args);

var connectionString = ConnectionStringHelper.Build();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/api/auth/logout";
        options.AccessDeniedPath = "/login";
        options.Cookie.Name = "jira.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    })
    .AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
    {
        options.ClientId =
            Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID")
            ?? builder.Configuration["Authentication:Google:ClientId"]
            ?? throw new InvalidOperationException("GOOGLE_CLIENT_ID not configured.");
        options.ClientSecret =
            Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET")
            ?? builder.Configuration["Authentication:Google:ClientSecret"]
            ?? throw new InvalidOperationException("GOOGLE_CLIENT_SECRET not configured.");

        options.CallbackPath = "/signin-google";
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.SaveTokens = true;
    })
    .AddGitHub(options =>
    {
        options.ClientId =
            Environment.GetEnvironmentVariable("GITHUB_CLIENT_ID")
            ?? builder.Configuration["Authentication:GitHub:ClientId"]
            ?? throw new InvalidOperationException("GITHUB_CLIENT_ID not configured.");
        options.ClientSecret =
            Environment.GetEnvironmentVariable("GITHUB_CLIENT_SECRET")
            ?? builder.Configuration["Authentication:GitHub:ClientSecret"]
            ?? throw new InvalidOperationException("GITHUB_CLIENT_SECRET not configured.");

        options.CallbackPath = "/signin-github";
        options.Scope.Add("user:email");
        options.SaveTokens = true;
    });

builder.Services.AddAuthorization();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddControllers();

var app = builder.Build();

await ApplyMigrationsAsync(app);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();


app.MapGet("/api/auth/login", (string provider, HttpContext ctx, [FromQuery] string? returnUrl) =>
{
    var redirectUrl = $"/api/auth/callback?provider={provider}&returnUrl={Uri.EscapeDataString(returnUrl ?? "/projects")}";
    var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
    return Results.Challenge(properties, [provider]);
});

app.MapGet("/api/auth/callback", async (
    string provider,
    string? returnUrl,
    HttpContext ctx,
    AppDbContext db) =>
{
    var result = await ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

    if (!result.Succeeded)
    {
        var extResult = await ctx.AuthenticateAsync(provider);
        if (!extResult.Succeeded)
            return Results.Redirect($"/login?error=oauth_failed");

        result = extResult;
    }

    var principal = result.Principal!;
    var claims = principal.Claims.ToDictionary(c => c.Type, c => c.Value);

    var email = claims.GetValueOrDefault("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")
             ?? claims.GetValueOrDefault("email")
             ?? claims.GetValueOrDefault("urn:github:email");

    if (string.IsNullOrWhiteSpace(email))
        return Results.Redirect("/login?error=no_email");

    var name = claims.GetValueOrDefault("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")
            ?? claims.GetValueOrDefault("name")
            ?? email.Split('@')[0];

    var avatar = claims.GetValueOrDefault("urn:github:avatar_url")
              ?? claims.GetValueOrDefault("picture")
              ?? claims.GetValueOrDefault("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/uri");

    var externalId = claims.GetValueOrDefault("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")
                  ?? claims.GetValueOrDefault("sub")
                  ?? claims.GetValueOrDefault("urn:github:id");

    // Upsert user in DB
    var user = await db.Uzytkownicy.FirstOrDefaultAsync(u => u.Email == email);
    if (user is null)
    {
        user = new jira.DbModels.Uzytkownik
        {
            Email = email,
            NazwaUzytkownika = name,
            AvatarUrl = avatar,
            PasswordHash = string.Empty,
            DataRejestracji = DateTime.UtcNow,
        };
        db.Uzytkownicy.Add(user);
    }
    else
    {
        user.NazwaUzytkownika = name;
        if (!string.IsNullOrEmpty(avatar)) user.AvatarUrl = avatar;
    }

    if (provider.Equals("Google", StringComparison.OrdinalIgnoreCase))
        user.GoogleId = externalId;
    else if (provider.Equals("GitHub", StringComparison.OrdinalIgnoreCase))
        user.GitHubId = externalId;

    await db.SaveChangesAsync();

    var appPrincipal = OAuthHelper.BuildPrincipal(user);
    await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, appPrincipal,
        new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
        });

    var safe = Uri.UnescapeDataString(returnUrl ?? "/projects");
    if (!safe.StartsWith('/')) safe = "/projects";
    return Results.Redirect(safe);
});

app.MapGet("/api/auth/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
});

app.MapStaticAssets();
app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

static async Task ApplyMigrationsAsync(WebApplication app)
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();
    try
    {
        await db.Database.MigrateAsync();
        logger.LogInformation("Database migrations applied successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while applying database migrations.");
    }
}
