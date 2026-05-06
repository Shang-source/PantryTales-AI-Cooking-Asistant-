using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using backend.Options;
using backend.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Resend;
using Xunit;

namespace backend.Tests.Services;

public class ResendEmailSenderTests
{
     private const string TestToEmail = "test@example.com";
     private const string TestSubject = "Test Subject";
     private const string TestBody = "<p>Test Body</p>";
     private const string TestFromEmail = "noreply@pantrytales.com";
     private const string TestFromName = "PantryTales";

     private static Mock<IResend> CreateMockResend(Action<EmailMessage>? captureMessage = null, Action<CancellationToken>? captureToken = null)
     {
          var mockResend = new Mock<IResend>();
          var mockResponse = new Mock<ResendResponse<Guid>>();
          mockResponse.Setup(r => r.Content).Returns(Guid.NewGuid());

          mockResend
              .Setup(x => x.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
              .Callback<EmailMessage, CancellationToken>((msg, token) =>
              {
                   captureMessage?.Invoke(msg);
                   captureToken?.Invoke(token);
              })
              .ReturnsAsync(mockResponse.Object);

          return mockResend;
     }

     [Fact]
     public async Task SendInvitationAsync_ConstructsEmailMessageCorrectly()
     {
          // Arrange
          EmailMessage? capturedMessage = null;
          var mockResend = new Mock<IResend>();
          mockResend
              .Setup(x => x.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
              .Callback<EmailMessage, CancellationToken>((msg, _) => capturedMessage = msg)
              .ReturnsAsync(CreateSuccessResponse());

          var options = CreateOptions();
          var logger = new FakeLogger<ResendEmailSender>();
          var sender = new ResendEmailSender(mockResend.Object, options, logger);

          // Act
          await sender.SendInvitationAsync(TestToEmail, TestSubject, TestBody);

          // Assert
          Assert.NotNull(capturedMessage);
          Assert.Equal($"{TestFromName} <{TestFromEmail}>", capturedMessage!.From);
          Assert.Single(capturedMessage.To);
          Assert.Equal(TestToEmail, capturedMessage.To[0]);
          Assert.Equal(TestSubject, capturedMessage.Subject);
          Assert.Equal(TestBody, capturedMessage.HtmlBody);
     }

     [Fact]
     public async Task SendInvitationAsync_PassesCancellationToken()
     {
          // Arrange
          CancellationToken capturedToken = default;
          var mockResend = new Mock<IResend>();
          mockResend
              .Setup(x => x.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
              .Callback<EmailMessage, CancellationToken>((_, token) => capturedToken = token)
              .ReturnsAsync(CreateSuccessResponse());

          var options = CreateOptions();
          var logger = new FakeLogger<ResendEmailSender>();
          var sender = new ResendEmailSender(mockResend.Object, options, logger);
          using var cts = new CancellationTokenSource();

          // Act
          await sender.SendInvitationAsync(TestToEmail, TestSubject, TestBody, cts.Token);

          // Assert
          Assert.Equal(cts.Token, capturedToken);
     }

     [Fact]
     public async Task SendInvitationAsync_LogsInformationOnSuccess()
     {
          // Arrange
          var mockResend = new Mock<IResend>();
          mockResend
              .Setup(x => x.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(CreateSuccessResponse());

          var options = CreateOptions();
          var logger = new FakeLogger<ResendEmailSender>();
          var sender = new ResendEmailSender(mockResend.Object, options, logger);

          // Act
          await sender.SendInvitationAsync(TestToEmail, TestSubject, TestBody);

          // Assert
          Assert.Single(logger.LoggedMessages);
          var logEntry = logger.LoggedMessages[0];
          Assert.Equal(LogLevel.Information, logEntry.Level);
          Assert.Contains(TestToEmail, logEntry.Message);
          Assert.Contains(TestSubject, logEntry.Message);
     }

     [Fact]
     public async Task SendInvitationAsync_LogsErrorAndRethrowsOnFailure()
     {
          // Arrange
          var expectedException = new InvalidOperationException("Resend API error");
          var mockResend = new Mock<IResend>();
          mockResend
              .Setup(x => x.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
              .ThrowsAsync(expectedException);

          var options = CreateOptions();
          var logger = new FakeLogger<ResendEmailSender>();
          var sender = new ResendEmailSender(mockResend.Object, options, logger);

          // Act & Assert
          var thrownException = await Assert.ThrowsAsync<InvalidOperationException>(
              () => sender.SendInvitationAsync(TestToEmail, TestSubject, TestBody));

          Assert.Same(expectedException, thrownException);
          Assert.Single(logger.LoggedMessages);
          var logEntry = logger.LoggedMessages[0];
          Assert.Equal(LogLevel.Error, logEntry.Level);
          Assert.Contains(TestToEmail, logEntry.Message);
          Assert.Contains(TestSubject, logEntry.Message);
          Assert.Same(expectedException, logEntry.Exception);
     }

     [Fact]
     public async Task SendInvitationAsync_UsesOptionsFromConfiguration()
     {
          // Arrange
          const string customFromEmail = "custom@example.com";
          const string customFromName = "Custom Sender";
          EmailMessage? capturedMessage = null;

          var mockResend = new Mock<IResend>();
          mockResend
              .Setup(x => x.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
              .Callback<EmailMessage, CancellationToken>((msg, _) => capturedMessage = msg)
              .ReturnsAsync(CreateSuccessResponse());

          var options = CreateOptions(customFromEmail, customFromName);
          var logger = new FakeLogger<ResendEmailSender>();
          var sender = new ResendEmailSender(mockResend.Object, options, logger);

          // Act
          await sender.SendInvitationAsync(TestToEmail, TestSubject, TestBody);

          // Assert
          Assert.NotNull(capturedMessage);
          Assert.Equal($"{customFromName} <{customFromEmail}>", capturedMessage!.From);
     }

     [Fact]
     public async Task SendInvitationAsync_CallsResendClientExactlyOnce()
     {
          // Arrange
          var mockResend = new Mock<IResend>();
          mockResend
              .Setup(x => x.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(CreateSuccessResponse());

          var options = CreateOptions();
          var logger = new FakeLogger<ResendEmailSender>();
          var sender = new ResendEmailSender(mockResend.Object, options, logger);

          // Act
          await sender.SendInvitationAsync(TestToEmail, TestSubject, TestBody);

          // Assert
          mockResend.Verify(
              x => x.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()),
              Times.Once);
     }

     private static IOptions<InvitationOptions> CreateOptions(
         string fromEmail = TestFromEmail,
         string fromName = TestFromName)
     {
          return Microsoft.Extensions.Options.Options.Create(new InvitationOptions
          {
               FromEmail = fromEmail,
               FromName = fromName,
               AcceptBaseUrl = "https://app.pantrytales.com"
          });
     }

     private static ResendResponse<Guid> CreateSuccessResponse()
     {
          return new ResendResponse<Guid>(Guid.NewGuid(), new ResendRateLimit());
     }

     #region Fakes

     private class FakeLogger<T> : ILogger<T>
     {
          public List<LogEntry> LoggedMessages { get; } = new();

          public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

          public bool IsEnabled(LogLevel logLevel) => true;

          public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
          {
               LoggedMessages.Add(new LogEntry
               {
                    Level = logLevel,
                    Message = formatter(state, exception),
                    Exception = exception
               });
          }

          public class LogEntry
          {
               public LogLevel Level { get; set; }
               public string Message { get; set; } = string.Empty;
               public Exception? Exception { get; set; }
          }
     }

     #endregion
}
