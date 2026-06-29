using System;
using System.Threading.Tasks;
using AIStudyHub.Business.Options;
using AIStudyHub.Business.Services;
using Xunit;

namespace AIStudyHub.Tests.Services;

public class EmailServiceTests
{
    [Fact]
    public async Task SendAsync_MissingConfig_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new SmtpOptions
        {
            Host = "",
            FromEmail = ""
        };
        var service = new EmailService(options);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SendAsync("test@test.com", "Subject", "Body"));
    }

    [Fact]
    public async Task SendAsync_ValidConfig_ThrowsSocketExceptionOrSucceeds()
    {
        // Arrange
        var options = new SmtpOptions
        {
            Host = "localhost", // Use dummy host
            Port = 2525,
            FromEmail = "noreply@aistudyhub.com",
            FromName = "AI Study Hub"
        };
        var service = new EmailService(options);

        // Act
        // Because we don't have a real SMTP server running on localhost:2525 during the test,
        // SendMailAsync will try to connect and eventually throw a SmtpException / SocketException.
        // If it throws SmtpException, it means the logic before the network call executed successfully.
        
        var ex = await Record.ExceptionAsync(() => 
            service.SendAsync("test@test.com", "Subject", "Body"));

        // Assert
        Assert.NotNull(ex);
        Assert.IsType<System.Net.Mail.SmtpException>(ex);
    }
}
