namespace jira.Services;

public class BoardStateService
{
    public event Func<Task>? OnBoardCreated;

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
