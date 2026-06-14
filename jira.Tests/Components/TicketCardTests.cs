using Bunit;
using jira.DbModels;
using jira.Tests.Fixtures;
using jira.Components.UI.TicketCard;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace jira.Tests.Components;

public class TicketCardTests : BunitContext
{
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

    [Fact]
    public void DisplayId_ForTaskId1_Returns6DigitNumber()
    {
        var task = MakeTask(id: 1);

        var cut = Render<TicketCard>(parameters => parameters
            .Add(p => p.Task, task));

        var idSpan = cut.Find("span.ticket-id");
        var text = idSpan.TextContent.TrimStart('#');
        Assert.True(int.TryParse(text, out int displayId),
            $"DisplayId powinien być liczbą całkowitą, otrzymano: '{text}'");
        Assert.InRange(displayId, 100_000, 999_999);
    }

    [Fact]
    public void DisplayId_ForSameTaskId_IsAlwaysSame()
    {
        var task1 = MakeTask(id: 42);
        var task2 = MakeTask(id: 42);

        var cut1 = Render<TicketCard>(p => p.Add(x => x.Task, task1));
        var cut2 = Render<TicketCard>(p => p.Add(x => x.Task, task2));

        var id1 = cut1.Find("span.ticket-id").TextContent.TrimStart('#');
        var id2 = cut2.Find("span.ticket-id").TextContent.TrimStart('#');
        Assert.Equal(id1, id2);
    }

    [Fact]
    public void DisplayId_ForDifferentTaskIds_ReturnsDifferentValues()
    {
        var task1 = MakeTask(id: 1);
        var task2 = MakeTask(id: 2);

        var cut1 = Render<TicketCard>(p => p.Add(x => x.Task, task1));
        var cut2 = Render<TicketCard>(p => p.Add(x => x.Task, task2));

        var id1 = cut1.Find("span.ticket-id").TextContent.TrimStart('#');
        var id2 = cut2.Find("span.ticket-id").TextContent.TrimStart('#');
        Assert.NotEqual(id1, id2);
    }

    [Theory]
    [InlineData("wysoki", "ticket-card--high")]
    [InlineData("high", "ticket-card--high")]
    [InlineData("niski", "ticket-card--low")]
    [InlineData("low", "ticket-card--low")]
    [InlineData("sredni", "ticket-card--medium")]
    [InlineData("medium", "ticket-card--medium")]
    [InlineData("WYSOKI", "ticket-card--high")]
    [InlineData("NISKI", "ticket-card--low")]
    [InlineData("unknown", "ticket-card--medium")]
    public void PriorityClass_ReturnsCorrectCssClass(string priority, string expectedClass)
    {
        var task = MakeTask(priority: priority);

        var cut = Render<TicketCard>(p => p.Add(x => x.Task, task));

        var card = cut.Find("div.ticket-card");
        Assert.Contains(expectedClass, card.ClassList);
    }

    [Theory]
    [InlineData("wysoki", "Wysoki")]
    [InlineData("high", "Wysoki")]
    [InlineData("niski", "Niski")]
    [InlineData("low", "Niski")]
    [InlineData("sredni", "Średni")]
    [InlineData("cokolwiek", "Średni")]
    public void PriorityLabel_ReturnsReadableLabel(string priority, string expectedLabel)
    {
        var task = MakeTask(priority: priority);

        var cut = Render<TicketCard>(p => p.Add(x => x.Task, task));

        var badge = cut.Find("span.ticket-priority-badge");
        Assert.Equal(expectedLabel, badge.TextContent.Trim());
    }

    [Theory]
    [InlineData(-30, "teraz")]
    [InlineData(-5 * 60, "5m")]
    [InlineData(-2 * 60 * 60, "2h")]
    [InlineData(-3 * 24 * 60 * 60, "3d")]
    [InlineData(-21 * 24 * 60 * 60, "3tyg")]
    [InlineData(-61 * 24 * 60 * 60, "2mies")]
    [InlineData(-400 * 24 * 60 * 60, "1l")]
    public void RelativeAge_ReturnsExpectedLabelForDifferentAges(
        int offsetInSeconds,
        string expectedLabel)
    {
        var task = MakeTask(
            createdAt: DateTime.UtcNow.AddSeconds(offsetInSeconds));

        var cut = Render<TicketCard>(p => p.Add(x => x.Task, task));

        var ageSpan = cut.Find("span.ticket-age");

        Assert.Equal(expectedLabel, ageSpan.TextContent.Trim());
    }

    [Fact]
    public void Render_WithValidTask_DisplaysTaskTitle()
    {
        const string title = "Moje super zadanie";
        var task = MakeTask();
        task.TytulZadania = title;

        var cut = Render<TicketCard>(p => p.Add(x => x.Task, task));

        var h6 = cut.Find("h6.ticket-title");
        Assert.Contains(title, h6.TextContent);
    }

    [Fact]
    public void Render_WithNoAssignedUser_DoesNotShowAssigneeElement()
    {
        var task = MakeTask();
        task.UzytkownikPrzypisany = null;

        var cut = Render<TicketCard>(p => p.Add(x => x.Task, task));

        var assignees = cut.FindAll("div.ticket-assignee");
        Assert.Empty(assignees);
    }

    [Fact]
    public void Render_WithHighPriority_HasDraggableAttribute()
    {
        var task = MakeTask(priority: "wysoki");

        var cut = Render<TicketCard>(p => p.Add(x => x.Task, task));

        var card = cut.Find("div.ticket-card");
        Assert.Equal("true", card.GetAttribute("draggable"));
    }

    [Fact]
    public async Task OnClick_WhenCardClicked_InvokesCallback()
    {
        var task = MakeTask(id: 7);
        Zadanie? receivedTask = null;

        var cut = Render<TicketCard>(p => p
            .Add(x => x.Task, task)
            .Add(x => x.OnClick, (Zadanie t) =>
            {
                receivedTask = t;
                return Task.CompletedTask;
            }));

        await cut.Find("div.ticket-card").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        Assert.NotNull(receivedTask);
        Assert.Equal(7, receivedTask!.IdZadania);
    }
}