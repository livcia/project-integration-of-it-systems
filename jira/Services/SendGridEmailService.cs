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
            Console.WriteLine("gowno? ");
            var subject = $"[Jira] Przypisano Ci zadanie: {taskTitle}";
            Console.WriteLine("gowno? ");
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
                        <div class="header">
                            <h1>System Jira – Nowe przypisanie zadania</h1>
                        </div>
                        <div class="body">
                            <h2>Cześć, {{System.Net.WebUtility.HtmlEncode(toName)}}!</h2>
                            <p>Zostało Ci przypisane nowe zadanie. Poniżej znajdziesz szczegóły:</p>
                            <div class="meta">
                                <p><span class="label">Tytuł zadania:</span> {{System.Net.WebUtility.HtmlEncode(taskTitle)}}</p>
                                <p><span class="label">ID zadania:</span> #{{taskId}}</p>
                                <p><span class="label">Tablica:</span> {{System.Net.WebUtility.HtmlEncode(boardName)}}</p>
                            </div>
                            <p><span class="label">Opis:</span></p>
                            {{descriptionHtml}}
                        </div>
                        <div class="footer">
                            Ta wiadomość została wygenerowana automatycznie przez System Jira. Prosimy nie odpowiadać na ten e-mail.
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

            if ((int)response.StatusCode >= 400)
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
            Console.WriteLine("weszlo? ");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send assignment notification email to {Email} for task #{TaskId}.", toEmail, taskId);
        }
    }
}
