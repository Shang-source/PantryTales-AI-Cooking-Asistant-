using backend.Interfaces;
using backend.Options;
using Microsoft.Extensions.Options;
using Resend;

namespace backend.Services;

/// <summary>
/// Email sender implementation using Resend API for production use.
/// </summary>
public class ResendEmailSender(
    IResend resend,
    IOptions<InvitationOptions> options,
    ILogger<ResendEmailSender> logger) : IEmailSender
{
     private readonly InvitationOptions _options = options.Value;

     public async Task SendInvitationAsync(
         string toEmail,
         string subject,
         string body,
         CancellationToken cancellationToken = default)
     {
          var message = new EmailMessage
          {
               From = $"{_options.FromName} <{_options.FromEmail}>",
               To = [toEmail],
               Subject = subject,
               HtmlBody = body
          };

          try
          {
               var response = await resend.EmailSendAsync(message, cancellationToken);

               logger.LogInformation(
                   "Email sent successfully via Resend. To: {ToEmail}, Subject: {Subject}, MessageId: {MessageId}",
                   toEmail,
                   subject,
                   response.Content);
          }
          catch (Exception ex)
          {
               logger.LogError(ex,
                   "Failed to send email via Resend. To: {ToEmail}, Subject: {Subject}",
                   toEmail,
                   subject);
               throw;
          }
     }
}
