namespace jira.Services;

/// <summary>
/// Scoped service that acts as a lightweight event bus between
/// ProjectCreate (publisher) and NavMenu (subscriber).
/// Blazor Server: scoped = one instance per SignalR circuit (user session).
/// </summary>
public class BoardStateService
{
    /// <summary>Raised when the current user successfully creates a new board.</summary>
    public event Func<Task>? OnBoardCreated;

    /// <summary>
    /// Called by ProjectCreate after a board is persisted to the DB.
    /// Notifies all subscribers (NavMenu) to refresh their board list.
    /// </summary>
    public async Task NotifyBoardCreatedAsync()
    {
        if (OnBoardCreated is not null)
        {
            foreach (var handler in OnBoardCreated.GetInvocationList().Cast<Func<Task>>())
            {
                await handler();
            }
        }
    }
}
