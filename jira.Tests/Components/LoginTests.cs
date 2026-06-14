using System;
using System.Threading.Tasks;
using Bunit;
using Xunit;
using jira.Components.Pages.Login; // Dostosuj namespace jeśli masz inną strukturę folderów

namespace jira.Tests.Components.Pages;

/// <summary>
/// Proste i stabilne testy bUnit dla ekranu logowania (Login.razor).
/// Weryfikują strukturę DOM, domyślne stany kontrolek oraz wbudowaną walidację pól.
/// </summary>
public class LoginTests : BunitContext
{
    // -----------------------------------------------------------------------
    // 1. Testy Renderowania i Struktury Początkowej
    // -----------------------------------------------------------------------

    [Fact]
    public void Render_InitialState_DisplaysCorrectHeadersAndSocialButtons()
    {
        // Act
        var cut = Render<Login>();

        // Assert - Nagłówek i Tytuł strony
        Assert.Equal("Jira Integration System", cut.Find("h1.h4").TextContent.Trim());
        Assert.Equal("Zaloguj się, aby kontynuować", cut.Find("p.text-muted").TextContent.Trim());

        // Assert - Sprawdzenie linków do logowania społecznościowego (OAuth)
        var googleLink = cut.Find("#btn-google-login");
        Assert.Equal("/api/auth/login?provider=Google", googleLink.GetAttribute("href"));

        var githubLink = cut.Find("#btn-github-login");
        Assert.Equal("/api/auth/login?provider=GitHub", githubLink.GetAttribute("href"));

        // Na początku nie powinno być żadnych komunikatów o błędach z backendu
        Assert.Empty(cut.FindAll(".alert-danger:not(form *)"));
    }

    // -----------------------------------------------------------------------
    // 2. Testy Walidacji Formularza (HTML / DataAnnotations)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Submit_EmptyForm_TriggersValidationErrors()
    {
        // Arrange
        var cut = Render<Login>();

        // Act - Próba wysłania pustego formularza
        var form = cut.Find("form");
        await form.SubmitAsync();

        // Assert - Szukamy wiadomości wygenerowanych przez DataAnnotationsValidator
        var validationMessages = cut.FindAll(".text-danger.small");
        
        // Powinny pojawić się błędy pod obydwoma polami
        Assert.True(validationMessages.Count >= 2);
        
        var errorTexts = validationMessages.Select(m => m.TextContent.Trim()).ToList();
        Assert.Contains("Adres e-mail jest wymagany.", errorTexts);
        Assert.Contains("Hasło jest wymagane.", errorTexts);
    }

    [Fact]
    public async Task Submit_InvalidEmailAndShortPassword_TriggersFormatValidationErrors()
    {
        // Arrange
        var cut = Render<Login>();

        // Act - Wpisanie niepoprawnego maila i zbyt krótkiego hasła
        cut.Find("#email").Change("zly_mail_bez_at");
        cut.Find("#password").Change("123");
        
        var form = cut.Find("form");
        await form.SubmitAsync();

        // Assert
        var validationMessages = cut.FindAll(".text-danger.small");
        var errorTexts = validationMessages.Select(m => m.TextContent.Trim()).ToList();

        Assert.Contains("Podaj prawidłowy adres e-mail.", errorTexts);
        Assert.Contains("Hasło musi mieć co najmniej 6 znaków.", errorTexts);
    }

    // -----------------------------------------------------------------------
    // 3. Test Logiki Biznesowej (Obsługa błędnego logowania)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Submit_ValidCredentials_ReturnsBackendErrorMessageAfterDelay()
    {
        // Arrange
        var cut = Render<Login>();

        // Act - Wprowadzamy poprawne dane pod kątem walidacji
        cut.Find("#email").Change("test@uzytkownik.pl");
        cut.Find("#password").Change("haslo123");

        // Wysyłamy formularz
        var form = cut.Find("form");
        await form.SubmitAsync();

        // Czekamy bezpiecznie, aż w drzewie DOM pojawi się ikona trójkąta ostrzegawczego (bi-exclamation-triangle-fill)
        // lub tekst błędu przypisany do zmiennej _errorMessage
        cut.WaitForState(() => cut.FindAll(".bi-exclamation-triangle-fill").Count > 0, TimeSpan.FromSeconds(2));

        // Assert - Wyciągamy ten konkretny alert, który zawiera ikonę błędu biznesowego
        var errorAlert = cut.Find("div.alert-danger"); 
    
        Assert.Contains("Nieprawidłowy e-mail lub hasło.", errorAlert.TextContent);
    }
}