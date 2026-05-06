namespace backend.Interfaces;

public interface IEmailSender
{
    Task SendInvitationAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default);
}
