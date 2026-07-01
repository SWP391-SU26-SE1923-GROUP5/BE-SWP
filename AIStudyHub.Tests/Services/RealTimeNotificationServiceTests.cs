using System;
using System.Threading;
using System.Threading.Tasks;
using AIStudyHub.Business.DTOs.Notifications;
using AIStudyHub.Business.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AIStudyHub.Tests.Services;

public class RealTimeNotificationServiceTests
{
    private readonly Mock<IHubContext<Hub>> _hubContextMock;
    private readonly Mock<IClientProxy> _clientProxyMock;
    private readonly Mock<ILogger<RealTimeNotificationService>> _loggerMock;
    private readonly RealTimeNotificationService _service;

    public RealTimeNotificationServiceTests()
    {
        _hubContextMock = new Mock<IHubContext<Hub>>();
        _clientProxyMock = new Mock<IClientProxy>();
        
        var clientsMock = new Mock<IHubClients>();
        clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(_clientProxyMock.Object);
        _hubContextMock.Setup(h => h.Clients).Returns(clientsMock.Object);

        _loggerMock = new Mock<ILogger<RealTimeNotificationService>>();
        _service = new RealTimeNotificationService(_hubContextMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task NotifyDocumentProcessedAsync_SendsNotificationToUserGroup()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        
        // Act
        await _service.NotifyDocumentProcessedAsync(userId, documentId, "Test Doc");

        // Assert
        _clientProxyMock.Verify(
            c => c.SendCoreAsync("ReceiveNotification", 
                It.Is<object[]>(args => 
                    args.Length == 1 && 
                    args[0] is RealTimeNotification &&
                    ((RealTimeNotification)args[0]).UserId == userId &&
                    ((RealTimeNotification)args[0]).Title == "Document processed"
                ), 
                It.IsAny<CancellationToken>()), 
            Times.Once);
    }
}
