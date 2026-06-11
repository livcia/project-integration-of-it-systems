using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace jira.Services;

public class SendGridEmailService : IEmailService
{
    private readonly ILogger<SendGridEmailService> _logger;

    public SendGridEmailService(ILogger<SendGridEmailService> logger)
    {
        _logger = logger;
    }

    public async Task SendAssignmentNotificationAsync(
        string toEmail,
        string toName,
        string taskTitle,
        string boardName,
        int taskId,
        string? taskDescription)
    {
        try
        {
            var apiKey = Environment.GetEnvironmentVariable("SENDGRID_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("SENDGRID_API_KEY is not configured. Skipping email notification.");
                return;
            }

            var fromEmail = Environment.GetEnvironmentVariable("SENDGRID_FROM_EMAIL");
            if (string.IsNullOrWhiteSpace(fromEmail))
            {
                _logger.LogWarning("SENDGRID_FROM_EMAIL is not configured. Skipping email notification.");
                return;
            }

            var fromName = Environment.GetEnvironmentVariable("SENDGRID_FROM_NAME") ?? "System Jira";

            var client = new SendGridClient(apiKey);

            var from = new EmailAddress(fromEmail, fromName);
            var to = new EmailAddress(toEmail, toName);

            var subject = $"[Jira] Przypisano Cię do zadania: {taskTitle}";

            var descriptionHtml = string.IsNullOrWhiteSpace(taskDescription)
                ? "<p><em>Brak opisu.</em></p>"
                : $"<p>{System.Net.WebUtility.HtmlEncode(taskDescription)}</p>";

            var htmlContent = $$"""
                <!DOCTYPE html>
                <html lang="pl">
                <head>
                    <meta charset="UTF-8" />
                    <style>
                        body { font-family: Arial, sans-serif; background-color: #f4f4f4; margin: 0; padding: 20px; }
                        .container { max-width: 600px; margin: 0 auto; background-color: #ffffff; border-radius: 8px; padding: 30px; box-shadow: 0 2px 6px rgba(0,0,0,0.1); }
                        h2 { color: #0052cc; }
                        .field-label { font-weight: bold; color: #555; margin-top: 16px; }
                        .field-value { margin: 4px 0 0 0; color: #222; }
                        .footer { margin-top: 30px; font-size: 12px; color: #999; border-top: 1px solid #eee; padding-top: 12px; }
                    </style>
                </head>
                <body>
                    <div class="container">
                        <h2>Zostałeś/aś przypisany/a do zadania</h2>
                        <p>Cześć, {{System.Net.WebUtility.HtmlEncode(toName)}}!</p>
                        <p>Przypisano Cię do nowego zadania w tablicy <strong>{{System.Net.WebUtility.HtmlEncode(boardName)}}</strong>.</p>

                        <div class="field-label">ID zadania:</div>
                        <div class="field-value">#{{taskId}}</div>

                        <div class="field-label">Tytuł zadania:</div>
                        <div class="field-value">{{System.Net.WebUtility.HtmlEncode(taskTitle)}}</div>

                        <div class="field-label">Opis:</div>
                        {{descriptionHtml}}

                        <div class="field-label">Tablica:</div>
                        <div class="field-value">{{System.Net.WebUtility.HtmlEncode(boardName)}}</div>

                        <div class="footer">
                            Wiadomość wygenerowana automatycznie przez System Jira. Prosimy nie odpowiadać na tego e-maila.
                        </div>
                    </div>
                </body>
                </html>
                """;

            var plainTextContent = $"Cześć, {toName}!\n\n"
                + $"Przypisano Cię do zadania #{taskId}: {taskTitle}\n"
                + $"Tablica: {boardName}\n"
                + $"Opis: {(string.IsNullOrWhiteSpace(taskDescription) ? "Brak opisu." : taskDescription)}\n";

            var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);

            var response = await client.SendEmailAsync(msg);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Assignment notification email sent successfully to {ToEmail} for task #{TaskId}.",
                    toEmail, taskId);
            }
            else
            {
                var body = await response.Body.ReadAsStringAsync();
                _logger.LogWarning(
                    "SendGrid returned non-success status {StatusCode} for task #{TaskId}. Body: {Body}",
                    response.StatusCode, taskId, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send assignment notification email to {ToEmail} for task #{TaskId}.",
                toEmail, taskId);
        }
    }
}
