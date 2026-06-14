namespace jira.Services;

public interface IEmailService
{
    Task SendAssignmentNotificationAsync(
        string toEmail,
        string toName,
        string taskTitle,
        string boardName,
        int taskId,
        string? taskDescription);
}
