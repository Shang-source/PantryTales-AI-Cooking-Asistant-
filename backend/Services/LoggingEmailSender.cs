using backend.Interfaces;

namespace backend.Services;

public class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendInvitationAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("[EmailStub] Sending invitation to {Email}. Subject: {Subject}. Body: {Body}",
            toEmail,
            subject,
            body);
        return Task.CompletedTask;
    }
}
