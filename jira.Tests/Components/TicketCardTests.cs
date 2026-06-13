using Bunit;
using jira.DbModels;
using jira.Tests.Fixtures;
using jira.Components.UI.TicketCard;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace jira.Tests.Components;

/// <summary>
/// Testy bUnit dla komponentu TicketCard – weryfikują właściwości
/// obliczeniowe (DisplayId, PriorityClass, PriorityLabel, RelativeAge)
/// oraz poprawność renderowania.
/// </summary>
public class TicketCardTests : BunitContext
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>Tworzy minimalny obiekt Zadanie gotowy do przekazania jako parametr.</summary>
    private static Zadanie MakeTask(
        int id = 1,
        string priority = "sredni",
        DateTime? createdAt = null) => new Zadanie
    {
        IdZadania = id,
        IdTablicy = 1,
        TytulZadania = "Testowe zadanie",
        Priorytet = priority,
        Status = "Todo",
        KolumnaTablicy = "Todo",
        IdUzytkownikaTworcyZadania = 1,
        DataStworzenia = createdAt ?? DateTime.UtcNow,
        Tablica = TestDataBuilder.CreateBoard(),
        TworcaZadania = TestDataBuilder.CreateUser()
    };

    // -----------------------------------------------------------------------
    // DisplayId – deterministyczny pseudo-losowy identyfikator
    // -----------------------------------------------------------------------

    [Fact]
    public void DisplayId_ForTaskId1_Returns6DigitNumber()
    {
        // Arrange
        var task = MakeTask(id: 1);

        // Act
        var cut = Render<TicketCard>(parameters => parameters
            .Add(p => p.Task, task));

        // Assert – DisplayId jest liczbą 6-cyfrową (100000–999999)
        var idSpan = cut.Find("span.ticket-id");
        var text = idSpan.TextContent.TrimStart('#');
        Assert.True(int.TryParse(text, out int displayId),
            $"DisplayId powinien być liczbą całkowitą, otrzymano: '{text}'");
        Assert.InRange(displayId, 100_000, 999_999);
    }

    [Fact]
    public void DisplayId_ForSameTaskId_IsAlwaysSame()
    {
        // Arrange
        var task1 = MakeTask(id: 42);
        var task2 = MakeTask(id: 42);

        // Act
        var cut1 = Render<TicketCard>(p => p.Add(x => x.Task, task1));
        var cut2 = Render<TicketCard>(p => p.Add(x => x.Task, task2));

        // Assert – ten sam id zadania → ten sam DisplayId (deterministyczność)
        var id1 = cut1.Find("span.ticket-id").TextContent.TrimStart('#');
        var id2 = cut2.Find("span.ticket-id").TextContent.TrimStart('#');
        Assert.Equal(id1, id2);
    }

    [Fact]
    public void DisplayId_ForDifferentTaskIds_ReturnsDifferentValues()
    {
        // Arrange
        var task1 = MakeTask(id: 1);
        var task2 = MakeTask(id: 2);

        // Act
        var cut1 = Render<TicketCard>(p => p.Add(x => x.Task, task1));
        var cut2 = Render<TicketCard>(p => p.Add(x => x.Task, task2));

        // Assert – różne id zadania → różne DisplayId
        var id1 = cut1.Find("span.ticket-id").TextContent.TrimStart('#');
        var id2 = cut2.Find("span.ticket-id").TextContent.TrimStart('#');
        Assert.NotEqual(id1, id2);
    }

    // -----------------------------------------------------------------------
    // PriorityClass – klasa CSS trafnie odzwierciedla priorytet
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("wysoki",   "ticket-card--high")]
    [InlineData("high",     "ticket-card--high")]
    [InlineData("niski",    "ticket-card--low")]
    [InlineData("low",      "ticket-card--low")]
    [InlineData("sredni",   "ticket-card--medium")]
    [InlineData("medium",   "ticket-card--medium")]
    [InlineData("WYSOKI",   "ticket-card--high")]   // case-insensitive
    [InlineData("NISKI",    "ticket-card--low")]
    [InlineData("unknown",  "ticket-card--medium")] // fallback
    public void PriorityClass_ReturnsCorrectCssClass(string priority, string expectedClass)
    {
        // Arrange
        var task = MakeTask(priority: priority);

        // Act
        var cut = Render<TicketCard>(p => p.Add(x => x.Task, task));

        // Assert – główny div karty powinien zawierać oczekiwaną klasę CSS
        var card = cut.Find("div.ticket-card");
        Assert.Contains(expectedClass, card.ClassList);
    }

    // -----------------------------------------------------------------------
    // PriorityLabel – czytelna etykieta dla użytkownika
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("wysoki", "Wysoki")]
    [InlineData("high",   "Wysoki")]
    [InlineData("niski",  "Niski")]
    [InlineData("low",    "Niski")]
    [InlineData("sredni", "Średni")]
    [InlineData("cokolwiek", "Średni")] // fallback
    public void PriorityLabel_ReturnsReadableLabel(string priority, string expectedLabel)
    {
        // Arrange
        var task = MakeTask(priority: priority);

        // Act
        var cut = Render<TicketCard>(p => p.Add(x => x.Task, task));

        // Assert – badge priorytetu zawiera oczekiwaną etykietę
        var badge = cut.Find("span.ticket-priority-badge");
        Assert.Equal(expectedLabel, badge.TextContent.Trim());
    }

    // -----------------------------------------------------------------------
    // RelativeAge – czytelny czas relatywny
    // -----------------------------------------------------------------------

    [Fact]
    public void RelativeAge_ForCreatedLessThanOneMinuteAgo_ReturnsTeraz()
    {
        // Arrange – zadanie stworzone 30 sekund temu
        var task = MakeTask(createdAt: DateTime.UtcNow.AddSeconds(-30));

        // Act
        var cut = Render<TicketCard>(p => p.Add(x => x.Task, task));

        // Assert
        var ageSpan = cut.Find("span.ticket-age");
        Assert.Equal("teraz", ageSpan.TextContent.Trim());
    }

    [Fact]
    public void RelativeAge_ForCreatedFiveMinutesAgo_ReturnsMLabel()
    {
        // Arrange – zadanie stworzone 5 minut temu
        var task = MakeTask(createdAt: DateTime.UtcNow.AddMinutes(-5));

        // Act
        var cut = Render<TicketCard>(p => p.Add(x => x.Task, task));

        // Assert – format "Xm", np. "5m"
        var ageSpan = cut.Find("span.ticket-age");
        Assert.Equal("5m", ageSpan.TextContent.Trim());
    }

    [Fact]
    public void RelativeAge_ForCreatedTwoHoursAgo_ReturnsHLabel()
    {
        // Arrange – zadanie stworzone 2 godziny temu
        var task = MakeTask(createdAt: DateTime.UtcNow.AddHours(-2));

        // Act
        var cut = Render<TicketCard>(p => p.Add(x => x.Task, task));

        // Assert – format "Xh", np. "2h"
        var ageSpan = cut.Find("span.ticket-age");
        Assert.Equal("2h", ageSpan.TextContent.Trim());
    }

    [Fact]
    public void RelativeAge_ForCreatedThreeDaysAgo_ReturnsDLabel()
    {
        // Arrange – zadanie stworzone 3 dni temu
        var task = MakeTask(createdAt: DateTime.UtcNow.AddDays(-3));

        // Act
        var cut = Render<TicketCard>(p => p.Add(x => x.Task, task));

        // Assert – format "Xd", np. "3d"
        var ageSpan = cut.Find("span.ticket-age");
        Assert.Equal("3d", ageSpan.TextContent.Trim());
    }

    [Fact]
    public void RelativeAge_ForCreatedThreeWeeksAgo_ReturnsTygLabel()
    {
        // Arrange – 21 dni = 3 tygodnie
        var task = MakeTask(createdAt: DateTime.UtcNow.AddDays(-21));

        // Act
        var cut = Render<TicketCard>(p => p.Add(x => x.Task, task));

        // Assert – format "Xtyg", np. "3tyg"
        var ageSpan = cut.Find("span.ticket-age");
        Assert.Equal("3tyg", ageSpan.TextContent.Trim());
    }

    [Fact]
    public void RelativeAge_ForCreatedTwoMonthsAgo_ReturnsMiesLabel()
    {
        // Arrange – 61 dni (> 60 dni, < 365 dni)
        var task = MakeTask(createdAt: DateTime.UtcNow.AddDays(-61));

        // Act
        var cut = Render<TicketCard>(p => p.Add(x => x.Task, task));

        // Assert – format "Xmies", np. "2mies"
        var ageSpan = cut.Find("span.ticket-age");
        Assert.Equal("2mies", ageSpan.TextContent.Trim());
    }

    [Fact]
    public void RelativeAge_ForCreatedMoreThanOneYearAgo_ReturnsLLabel()
    {
        // Arrange – 400 dni (> 365 dni)
        var task = MakeTask(createdAt: DateTime.UtcNow.AddDays(-400));

        // Act
        var cut = Render<TicketCard>(p => p.Add(x => x.Task, task));

        // Assert – format "Xl", np. "1l"
        var ageSpan = cut.Find("span.ticket-age");
        Assert.Equal("1l", ageSpan.TextContent.Trim());
    }

    // -----------------------------------------------------------------------
    // Renderowanie – ogólna poprawność struktury HTML
    // -----------------------------------------------------------------------

    [Fact]
    public void Render_WithValidTask_RendersWithoutException()
    {
        // Arrange
        var task = MakeTask();

        // Act & Assert – brak wyjątku przy renderowaniu
        var exception = Record.Exception(() =>
            Render<TicketCard>(p => p.Add(x => x.Task, task)));
        Assert.Null(exception);
    }

    [Fact]
    public void Render_WithValidTask_DisplaysTaskTitle()
    {
        // Arrange
        const string title = "Moje super zadanie";
        var task = MakeTask();
        task.TytulZadania = title;

        // Act
        var cut = Render<TicketCard>(p => p.Add(x => x.Task, task));

        // Assert – tytuł zadania pojawia się w DOM
        var h6 = cut.Find("h6.ticket-title");
        Assert.Contains(title, h6.TextContent);
    }

    [Fact]
    public void Render_WithAssignedUser_ShowsAvatarOrInitial()
    {
        // Arrange
        var user = TestDataBuilder.CreateUser(id: 5, username: "jkowalski");
        var task = MakeTask();
        task.UzytkownikPrzypisany = user;

        // Act
        var cut = Render<TicketCard>(p => p.Add(x => x.Task, task));

        // Assert – element awatara istnieje
        var assignee = cut.Find("div.ticket-assignee");
        Assert.NotNull(assignee);
    }

    [Fact]
    public void Render_WithNoAssignedUser_DoesNotShowAssigneeElement()
    {
        // Arrange
        var task = MakeTask();
        task.UzytkownikPrzypisany = null;

        // Act
        var cut = Render<TicketCard>(p => p.Add(x => x.Task, task));

        // Assert – brak elementu awatara gdy nikt nie jest przypisany
        var assignees = cut.FindAll("div.ticket-assignee");
        Assert.Empty(assignees);
    }

    [Fact]
    public void Render_WithHighPriority_HasDraggableAttribute()
    {
        // Arrange
        var task = MakeTask(priority: "wysoki");

        // Act
        var cut = Render<TicketCard>(p => p.Add(x => x.Task, task));

        // Assert – karta jest przeciągalna
        var card = cut.Find("div.ticket-card");
        Assert.Equal("true", card.GetAttribute("draggable"));
    }

    // -----------------------------------------------------------------------
    // Callbacks – weryfikacja wywołania EventCallback
    // -----------------------------------------------------------------------

    [Fact]
    public async Task OnClick_WhenCardClicked_InvokesCallback()
    {
        // Arrange
        var task = MakeTask(id: 7);
        Zadanie? receivedTask = null;

        var cut = Render<TicketCard>(p => p
            .Add(x => x.Task, task)
            .Add(x => x.OnClick, (Zadanie t) =>
            {
                receivedTask = t;
                return Task.CompletedTask;
            }));

        // Act
        await cut.Find("div.ticket-card").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Assert
        Assert.NotNull(receivedTask);
        Assert.Equal(7, receivedTask!.IdZadania);
    }
}
