using System;
using System.Threading;
using System.Threading.Tasks;
using AIStudyHub.Business.DTOs.Notifications;
using AIStudyHub.Business.Hubs;
using AIStudyHub.Business.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AIStudyHub.Tests.Services;

public class RealTimeNotificationServiceTests
{
    private readonly Mock<IHubContext<NotificationsHub>> _hubContextMock;
    private readonly Mock<IClientProxy> _clientProxyMock;
    private readonly Mock<ILogger<RealTimeNotificationService>> _loggerMock;
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly RealTimeNotificationService _service;

    public RealTimeNotificationServiceTests()
    {
        _hubContextMock = new Mock<IHubContext<NotificationsHub>>();
        _clientProxyMock = new Mock<IClientProxy>();
        
        var clientsMock = new Mock<IHubClients>();
        clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(_clientProxyMock.Object);
        _hubContextMock.Setup(h => h.Clients).Returns(clientsMock.Object);

        _loggerMock = new Mock<ILogger<RealTimeNotificationService>>();

        // Mock IServiceScopeFactory to return a scope whose ServiceProvider throws,
        // so SendNotificationAsync skips DB persistence (caught by the try/catch).
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(new Mock<IServiceProvider>().Object);
        _scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

        _service = new RealTimeNotificationService(
            _hubContextMock.Object,
            _scopeFactoryMock.Object,
            _loggerMock.Object);
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

