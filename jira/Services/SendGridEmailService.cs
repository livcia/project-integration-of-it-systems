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
            var subject = $"[Jira] Przypisano Ci zadanie: {taskTitle}";
            var descriptionHtml = string.IsNullOrWhiteSpace(taskDescription)
                ? "<p><em>Brak opisu.</em></p>"
                : $"<p>{System.Net.WebUtility.HtmlEncode(taskDescription).Replace("\n", "<br/>")}</p>";

            var htmlContent = $$"""
                <!DOCTYPE html>
                <html lang="pl">
                <head>
                    <meta charset="UTF-8" />
                    <style>
                        body { font-family: Arial, sans-serif; background-color: #f4f6f8; margin: 0; padding: 0; }
                        .container { max-width: 600px; margin: 40px auto; background: #ffffff; border-radius: 8px; overflow: hidden; box-shadow: 0 2px 8px rgba(0,0,0,0.1); }
                        .header { background-color: #0052cc; padding: 24px 32px; }
                        .header h1 { color: #ffffff; margin: 0; font-size: 20px; }
                        .body { padding: 32px; color: #172b4d; }
                        .body h2 { font-size: 18px; margin-top: 0; }
                        .meta { background-color: #f4f6f8; border-radius: 6px; padding: 16px; margin: 16px 0; font-size: 14px; }
                        .meta p { margin: 4px 0; }
                        .label { font-weight: bold; color: #5e6c84; }
                        .footer { padding: 16px 32px; font-size: 12px; color: #97a0af; border-top: 1px solid #dfe1e6; }
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

            var plainText = $"Cześć {toName},\n\nZostało Ci przypisane zadanie:\n" +
                            $"Tytuł: {taskTitle}\n" +
                            $"ID: #{taskId}\n" +
                            $"Tablica: {boardName}\n" +
                            $"Opis: {taskDescription ?? "Brak opisu."}\n";

            var msg = MailHelper.CreateSingleEmail(from, to, subject, plainText, htmlContent);

            var response = await client.SendEmailAsync(msg);

            if ((int)response.StatusCode >= (int)System.Net.HttpStatusCode.BadRequest)
            {
                var body = await response.Body.ReadAsStringAsync();
                _logger.LogError(
                    "SendGrid returned error status {StatusCode} when sending to {Email}. Body: {Body}",
                    response.StatusCode, toEmail, body);
            }
            else
            {
                _logger.LogInformation(
                    "Assignment notification sent successfully to {Email} for task #{TaskId}.",
                    toEmail, taskId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send assignment notification email to {Email} for task #{TaskId}.", toEmail, taskId);
        }
    }
}
