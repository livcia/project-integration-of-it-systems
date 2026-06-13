using Bunit;
using jira.DbModels;
using jira.Tests.Fixtures;
using jira.Components.UI.StatusColumn;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace jira.Tests.Components;

/// <summary>
/// Testy bUnit dla komponentu StatusColumn – weryfikują renderowanie
/// listy zadań, a także zachowanie podczas operacji drag-and-drop
/// (HandleDragOver, HandleDragLeave, HandleDrop).
/// </summary>
public class StatusColumnTests : BunitContext
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Renderuje StatusColumn z opcjonalną zawartością i ColumnKey.
    /// </summary>
    private IRenderedComponent<StatusColumn> RenderColumn(
        string title = "Do zrobienia",
        string columnKey = "Todo",
        string? childMarkup = null,
        EventCallback<string>? onDrop = null)
    {
        return Render<StatusColumn>(p =>
        {
            p.Add(x => x.Title, title);
            p.Add(x => x.ColumnKey, columnKey);

            if (childMarkup is not null)
                p.AddChildContent(childMarkup);

            if (onDrop.HasValue)
                p.Add(x => x.OnDrop, onDrop.Value);
        });
    }

    // -----------------------------------------------------------------------
    // Renderowanie podstawowe
    // -----------------------------------------------------------------------

    [Fact]
    public void Render_WithTitle_DisplaysTitle()
    {
        // Arrange & Act
        var cut = RenderColumn(title: "W toku");

        // Assert
        var h5 = cut.Find("h5.kanban-col-title");
        Assert.Contains("W toku", h5.TextContent);
    }

    [Fact]
    public void Render_WithChildContent_DisplaysChildMarkup()
    {
        // Arrange & Act – wstrzykujemy prosty markup jako ChildContent
        var cut = RenderColumn(childMarkup: "<div class=\"test-ticket\">Ticket A</div>");

        // Assert – dziecko pojawia się w DOM
        var ticket = cut.Find("div.test-ticket");
        Assert.NotNull(ticket);
        Assert.Contains("Ticket A", ticket.TextContent);
    }

    [Fact]
    public void Render_WithoutChildContent_ShowsEmptyCardArea()
    {
        // Arrange & Act
        var cut = RenderColumn();

        // Assert – kolumna renderuje się, obszar kart istnieje
        var cards = cut.Find("div.kanban-col-cards");
        Assert.NotNull(cards);
    }

    [Fact]
    public void Render_WithDefaultParameters_NoDragOverStyleInitially()
    {
        // Arrange & Act
        var cut = RenderColumn();

        // Assert – bez aktywnego drag-over nie ma klasy wyróżnienia
        var inner = cut.Find("div.kanban-col-inner");
        Assert.DoesNotContain("kanban-col-inner--drag-over", inner.ClassList);
    }

    // -----------------------------------------------------------------------
    // HandleDragOver – ustawia IsDragOver = true
    // -----------------------------------------------------------------------

    [Fact]
    public void HandleDragOver_WhenTriggered_AddsDragOverClass()
    {
        // Arrange
        var cut = RenderColumn();
        var inner = cut.Find("div.kanban-col-inner");

        // Act – symulujemy zdarzenie dragover
        inner.TriggerEvent("ondragover", new DragEventArgs());

        // Assert – klasa drag-over powinna pojawić się po zdarzeniu
        inner = cut.Find("div.kanban-col-inner");
        Assert.Contains("kanban-col-inner--drag-over", inner.ClassList);
    }

    [Fact]
    public void HandleDragOver_WhenTriggered_ShowsDropPlaceholder()
    {
        // Arrange
        var cut = RenderColumn();

        // Act
        cut.Find("div.kanban-col-inner").TriggerEvent("ondragover", new DragEventArgs());

        // Assert – placeholder pojawia się gdy IsDragOver = true
        var placeholder = cut.FindAll("div.kanban-drop-placeholder");
        Assert.NotEmpty(placeholder);
    }

    // -----------------------------------------------------------------------
    // HandleDragLeave – resetuje IsDragOver = false
    // -----------------------------------------------------------------------

    [Fact]
    public void HandleDragLeave_AfterDragOver_RemovesDragOverClass()
    {
        // Arrange – najpierw symulujemy dragover (IsDragOver = true)
        var cut = RenderColumn();
        var inner = cut.Find("div.kanban-col-inner");
        inner.TriggerEvent("ondragover", new DragEventArgs());

        // Sprawdź że drag-over jest aktywny
        inner = cut.Find("div.kanban-col-inner");
        Assert.Contains("kanban-col-inner--drag-over", inner.ClassList);

        // Act – symulujemy dragleave
        inner.TriggerEvent("ondragleave", new DragEventArgs());

        // Assert – klasa drag-over powinna zniknąć
        inner = cut.Find("div.kanban-col-inner");
        Assert.DoesNotContain("kanban-col-inner--drag-over", inner.ClassList);
    }

    [Fact]
    public void HandleDragLeave_AfterDragOver_HidesDropPlaceholder()
    {
        // Arrange
        var cut = RenderColumn();
        var inner = cut.Find("div.kanban-col-inner");
        inner.TriggerEvent("ondragover", new DragEventArgs());

        // Placeholder widoczny
        Assert.NotEmpty(cut.FindAll("div.kanban-drop-placeholder"));

        // Act
        inner = cut.Find("div.kanban-col-inner");
        inner.TriggerEvent("ondragleave", new DragEventArgs());

        // Assert – placeholder zniknął
        Assert.Empty(cut.FindAll("div.kanban-drop-placeholder"));
    }

    [Fact]
    public void HandleDragLeave_WithoutPreviousDragOver_DoesNotThrow()
    {
        // Arrange
        var cut = RenderColumn();
        var inner = cut.Find("div.kanban-col-inner");

        // Act & Assert – brak wyjątku gdy dragleave bez wcześniejszego dragover
        var exception = Record.Exception(() =>
            inner.TriggerEvent("ondragleave", new DragEventArgs()));
        Assert.Null(exception);
    }

    // -----------------------------------------------------------------------
    // HandleDrop – resetuje styl i wywołuje OnDrop callback
    // -----------------------------------------------------------------------

    [Fact]
    public async Task HandleDrop_WhenTriggered_RemovesDragOverClass()
    {
        // Arrange
        var cut = RenderColumn(columnKey: "InProgress");
        var inner = cut.Find("div.kanban-col-inner");
        inner.TriggerEvent("ondragover", new DragEventArgs());

        // Upewnij się że drag-over jest aktywny
        inner = cut.Find("div.kanban-col-inner");
        Assert.Contains("kanban-col-inner--drag-over", inner.ClassList);

        // Act – upuszczamy element
        await inner.TriggerEventAsync("ondrop", new DragEventArgs());

        // Assert – po upuszczeniu drag-over znika
        inner = cut.Find("div.kanban-col-inner");
        Assert.DoesNotContain("kanban-col-inner--drag-over", inner.ClassList);
    }

    [Fact]
    public async Task HandleDrop_WhenTriggered_InvokesOnDropCallbackWithColumnKey()
    {
        // Arrange
        const string expectedKey = "Done";
        string? receivedKey = null;

        var onDropCallback = EventCallback.Factory.Create<string>(this, (key) =>
        {
            receivedKey = key;
        });

        var cut = Render<StatusColumn>(p => p
            .Add(x => x.Title, "Gotowe")
            .Add(x => x.ColumnKey, expectedKey)
            .Add(x => x.OnDrop, onDropCallback));

        // Act
        await cut.Find("div.kanban-col-inner").TriggerEventAsync("ondrop", new DragEventArgs());

        // Assert – callback otrzymał poprawny klucz kolumny
        Assert.Equal(expectedKey, receivedKey);
    }

    // -----------------------------------------------------------------------
    // Sekwencje zdarzeń drag-and-drop
    // -----------------------------------------------------------------------

    [Fact]
    public void DragOver_ThenDragLeave_ThenDragOver_StyleIsCorrect()
    {
        // Arrange
        var cut = RenderColumn();
        var inner = cut.Find("div.kanban-col-inner");

        // Act – pełna sekwencja: over → leave → over
        inner.TriggerEvent("ondragover", new DragEventArgs());
        inner = cut.Find("div.kanban-col-inner");
        Assert.Contains("kanban-col-inner--drag-over", inner.ClassList);

        inner.TriggerEvent("ondragleave", new DragEventArgs());
        inner = cut.Find("div.kanban-col-inner");
        Assert.DoesNotContain("kanban-col-inner--drag-over", inner.ClassList);

        inner.TriggerEvent("ondragover", new DragEventArgs());
        inner = cut.Find("div.kanban-col-inner");
        Assert.Contains("kanban-col-inner--drag-over", inner.ClassList);
    }

    // -----------------------------------------------------------------------
    // Właściwości parametrów
    // -----------------------------------------------------------------------

    [Fact]
    public void Title_Default_IsKolumna()
    {
        // Arrange & Act – bez podania tytułu
        var cut = Render<StatusColumn>();

        // Assert – domyślny tytuł
        var h5 = cut.Find("h5.kanban-col-title");
        Assert.Contains("Kolumna", h5.TextContent);
    }

    [Fact]
    public async Task ColumnKey_Default_IsEmptyString()
    {
        // Arrange – renderujemy bez ColumnKey
        string? receivedKey = null;
        var callback = EventCallback.Factory.Create<string>(this, (k) => receivedKey = k);

        var cut = Render<StatusColumn>(p => p
            .Add(x => x.OnDrop, callback));

        // Act
        await cut.Find("div.kanban-col-inner").TriggerEventAsync("ondrop", new DragEventArgs());

        // Assert – domyślny ColumnKey to ""
        Assert.Equal(string.Empty, receivedKey);
    }
}
