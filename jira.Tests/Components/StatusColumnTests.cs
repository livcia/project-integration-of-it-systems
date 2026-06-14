using Bunit;
using jira.DbModels;
using jira.Tests.Fixtures;
using jira.Components.UI.StatusColumn;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace jira.Tests.Components;

public class StatusColumnTests : BunitContext
{
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

    [Fact]
    public void Render_WithTitle_DisplaysTitle()
    {
        var cut = RenderColumn(title: "W toku");

        var h5 = cut.Find("h5.kanban-col-title");
        Assert.Contains("W toku", h5.TextContent);
    }

    [Fact]
    public void Render_WithChildContent_DisplaysChildMarkup()
    {
        var cut = RenderColumn(childMarkup: "<div class=\"test-ticket\">Ticket A</div>");

        var ticket = cut.Find("div.test-ticket");
        Assert.NotNull(ticket);
        Assert.Contains("Ticket A", ticket.TextContent);
    }

    [Fact]
    public void Render_WithDefaultParameters_NoDragOverStyleInitially()
    {
        var cut = RenderColumn();

        var inner = cut.Find("div.kanban-col-inner");
        Assert.DoesNotContain("kanban-col-inner--drag-over", inner.ClassList);
    }

    [Fact]
    public void HandleDragOver_WhenTriggered_AddsDragOverClass()
    {
        var cut = RenderColumn();
        var inner = cut.Find("div.kanban-col-inner");

        inner.TriggerEvent("ondragover", new DragEventArgs());

        inner = cut.Find("div.kanban-col-inner");
        Assert.Contains("kanban-col-inner--drag-over", inner.ClassList);
    }

    [Fact]
    public void HandleDragOver_WhenTriggered_ShowsDropPlaceholder()
    {
        var cut = RenderColumn();

        cut.Find("div.kanban-col-inner").TriggerEvent("ondragover", new DragEventArgs());

        var placeholder = cut.FindAll("div.kanban-drop-placeholder");
        Assert.NotEmpty(placeholder);
    }

    [Fact]
    public void HandleDragLeave_AfterDragOver_RemovesDragOverClass()
    {
        var cut = RenderColumn();
        var inner = cut.Find("div.kanban-col-inner");
        inner.TriggerEvent("ondragover", new DragEventArgs());

        inner = cut.Find("div.kanban-col-inner");
        Assert.Contains("kanban-col-inner--drag-over", inner.ClassList);

        inner.TriggerEvent("ondragleave", new DragEventArgs());

        inner = cut.Find("div.kanban-col-inner");
        Assert.DoesNotContain("kanban-col-inner--drag-over", inner.ClassList);
    }

    [Fact]
    public void HandleDragLeave_AfterDragOver_HidesDropPlaceholder()
    {
        var cut = RenderColumn();
        var inner = cut.Find("div.kanban-col-inner");
        inner.TriggerEvent("ondragover", new DragEventArgs());

        Assert.NotEmpty(cut.FindAll("div.kanban-drop-placeholder"));

        inner = cut.Find("div.kanban-col-inner");
        inner.TriggerEvent("ondragleave", new DragEventArgs());

        Assert.Empty(cut.FindAll("div.kanban-drop-placeholder"));
    }


    [Fact]
    public async Task HandleDrop_WhenTriggered_RemovesDragOverClass()
    {
        var cut = RenderColumn(columnKey: "InProgress");
        var inner = cut.Find("div.kanban-col-inner");
        inner.TriggerEvent("ondragover", new DragEventArgs());

        inner = cut.Find("div.kanban-col-inner");
        Assert.Contains("kanban-col-inner--drag-over", inner.ClassList);

        await inner.TriggerEventAsync("ondrop", new DragEventArgs());

        inner = cut.Find("div.kanban-col-inner");
        Assert.DoesNotContain("kanban-col-inner--drag-over", inner.ClassList);
    }

    [Fact]
    public async Task HandleDrop_WhenTriggered_InvokesOnDropCallbackWithColumnKey()
    {
        const string expectedKey = "Done";
        string? receivedKey = null;

        var onDropCallback = EventCallback.Factory.Create<string>(this, (key) => { receivedKey = key; });

        var cut = Render<StatusColumn>(p => p
            .Add(x => x.Title, "Gotowe")
            .Add(x => x.ColumnKey, expectedKey)
            .Add(x => x.OnDrop, onDropCallback));

        await cut.Find("div.kanban-col-inner").TriggerEventAsync("ondrop", new DragEventArgs());

        Assert.Equal(expectedKey, receivedKey);
    }

    [Fact]
    public void Title_Default_IsKolumna()
    {
        var cut = Render<StatusColumn>();

        var h5 = cut.Find("h5.kanban-col-title");
        Assert.Contains("Kolumna", h5.TextContent);
    }

    [Fact]
    public async Task ColumnKey_Default_IsEmptyString()
    {
        string? receivedKey = null;
        var callback = EventCallback.Factory.Create<string>(this, (k) => receivedKey = k);

        var cut = Render<StatusColumn>(p => p
            .Add(x => x.OnDrop, callback));

        await cut.Find("div.kanban-col-inner").TriggerEventAsync("ondrop", new DragEventArgs());

        Assert.Equal(string.Empty, receivedKey);
    }
}