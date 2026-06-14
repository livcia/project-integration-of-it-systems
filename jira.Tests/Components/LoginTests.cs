using System;
using System.Threading.Tasks;
using Bunit;
using Xunit;
using jira.Components.Pages.Login;

namespace jira.Tests.Components.Pages;

public class LoginTests : BunitContext
{
    [Fact]
    public void Render_InitialState_DisplaysCorrectHeadersAndSocialButtons()
    {
        var cut = Render<Login>();

        Assert.Equal("Jira Integration System", cut.Find("h1.h4").TextContent.Trim());
        Assert.Equal("Zaloguj się, aby kontynuować", cut.Find("p.text-muted").TextContent.Trim());

        var googleLink = cut.Find("#btn-google-login");
        Assert.Equal("/api/auth/login?provider=Google", googleLink.GetAttribute("href"));

        var githubLink = cut.Find("#btn-github-login");
        Assert.Equal("/api/auth/login?provider=GitHub", githubLink.GetAttribute("href"));

        Assert.Empty(cut.FindAll(".alert-danger:not(form *)"));
    }

    [Fact]
    public async Task Submit_EmptyForm_TriggersValidationErrors()
    {
        var cut = Render<Login>();

        var form = cut.Find("form");
        await form.SubmitAsync();

        var validationMessages = cut.FindAll(".text-danger.small");

        Assert.Equal(2, validationMessages.Count);

        var errorTexts = validationMessages.Select(m => m.TextContent.Trim()).ToList();
        Assert.Contains("Adres e-mail jest wymagany.", errorTexts);
        Assert.Contains("Hasło jest wymagane.", errorTexts);
    }

    [Fact]
    public async Task Submit_InvalidEmailAndShortPassword_TriggersFormatValidationErrors()
    {
        var cut = Render<Login>();

        cut.Find("#email").Change("zly_mail_bez_at");
        cut.Find("#password").Change("123");

        var form = cut.Find("form");
        await form.SubmitAsync();

        var validationMessages = cut.FindAll(".text-danger.small");
        var errorTexts = validationMessages.Select(m => m.TextContent.Trim()).ToList();

        Assert.Contains("Podaj prawidłowy adres e-mail.", errorTexts);
        Assert.Contains("Hasło musi mieć co najmniej 6 znaków.", errorTexts);
    }

    [Fact]
    public async Task Submit_ValidCredentials_ReturnsBackendErrorMessageAfterDelay()
    {
        var cut = Render<Login>();

        cut.Find("#email").Change("test@uzytkownik.pl");
        cut.Find("#password").Change("haslo123");

        var form = cut.Find("form");
        await form.SubmitAsync();

        cut.WaitForState(() => cut.FindAll(".bi-exclamation-triangle-fill").Count > 0, TimeSpan.FromSeconds(2));

        var errorAlert = cut.Find("div.alert-danger");

        Assert.Contains("Nieprawidłowy e-mail lub hasło.", errorAlert.TextContent);
    }
}