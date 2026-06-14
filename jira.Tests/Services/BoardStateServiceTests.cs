using Xunit;
using jira.Services;

namespace jira.Tests.Services;

/// <summary>
/// Testy jednostkowe dla BoardStateService – lekkiego event busa
/// między komponentami Blazor Server.
/// </summary>
public class BoardStateServiceTests
{
    // -----------------------------------------------------------------------
    // NotifyBoardCreatedAsync – brak subskrybentów
    // -----------------------------------------------------------------------

    [Fact]
    public async Task NotifyBoardCreatedAsync_WhenNoSubscribers_DoesNotThrow()
    {
        // Arrange
        var service = new BoardStateService();

        // Act & Assert – brak wyjątku
        var exception = await Record.ExceptionAsync(() => service.NotifyBoardCreatedAsync());
        Assert.Null(exception);
    }

    // -----------------------------------------------------------------------
    // NotifyBoardCreatedAsync – jeden subskrybent
    // -----------------------------------------------------------------------

    [Fact]
    public async Task NotifyBoardCreatedAsync_WithOneSubscriber_CallsHandler()
    {
        // Arrange
        var service = new BoardStateService();
        var callCount = 0;

        service.OnBoardCreated += () =>
        {
            callCount++;
            return Task.CompletedTask;
        };

        // Act
        await service.NotifyBoardCreatedAsync();

        // Assert
        Assert.Equal(1, callCount);
    }

    // -----------------------------------------------------------------------
    // NotifyBoardCreatedAsync – kilku subskrybentów
    // -----------------------------------------------------------------------

    [Fact]
    public async Task NotifyBoardCreatedAsync_WithMultipleSubscribers_CallsAllHandlers()
    {
        // Arrange
        var service = new BoardStateService();
        var results = new List<string>();

        service.OnBoardCreated += () =>
        {
            results.Add("handler1");
            return Task.CompletedTask;
        };
        service.OnBoardCreated += () =>
        {
            results.Add("handler2");
            return Task.CompletedTask;
        };
        service.OnBoardCreated += () =>
        {
            results.Add("handler3");
            return Task.CompletedTask;
        };

        // Act
        await service.NotifyBoardCreatedAsync();

        // Assert – wszyscy handlerzy zostali wywołani
        Assert.Equal(3, results.Count);
        Assert.Contains("handler1", results);
        Assert.Contains("handler2", results);
        Assert.Contains("handler3", results);
    }

    // -----------------------------------------------------------------------
    // OnBoardCreated – rejestracja subskrybenta
    // -----------------------------------------------------------------------

    [Fact]
    public async Task OnBoardCreated_AfterSubscription_HandlerIsInvoked()
    {
        // Arrange
        var service = new BoardStateService();
        var wasInvoked = false;

        Func<Task> handler = () =>
        {
            wasInvoked = true;
            return Task.CompletedTask;
        };

        service.OnBoardCreated += handler;

        // Act
        await service.NotifyBoardCreatedAsync();

        // Assert
        Assert.True(wasInvoked);
    }

    // -----------------------------------------------------------------------
    // OnBoardCreated – wyrejestrowanie subskrybenta
    // -----------------------------------------------------------------------

    [Fact]
    public async Task OnBoardCreated_AfterUnsubscription_HandlerIsNotInvoked()
    {
        // Arrange
        var service = new BoardStateService();
        var callCount = 0;

        Func<Task> handler = () =>
        {
            callCount++;
            return Task.CompletedTask;
        };

        service.OnBoardCreated += handler;
        service.OnBoardCreated -= handler;

        // Act
        await service.NotifyBoardCreatedAsync();

        // Assert – handler nie powinien zostać wywołany po wyrejestrowaniu
        Assert.Equal(0, callCount);
    }
}
