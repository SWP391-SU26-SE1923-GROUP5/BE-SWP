using AIStudyHub.Business.Services;
using AIStudyHub.Business.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AIStudyHub.Tests.Services;

public class BusinessHostedServiceRegistrationTests
{
    [Fact]
    public void AddBusinessHostedServices_RegistersEachWorkerExactlyOnce()
    {
        var services = new ServiceCollection();

        services.AddBusinessHostedServices();

        var hostedServices = services
            .Where(descriptor => descriptor.ServiceType == typeof(IHostedService))
            .ToList();
        var expectedWorkerTypes = new[]
        {
            typeof(DocumentBackgroundProcessor),
            typeof(TierExpiryWorker),
            typeof(UnverifiedAccountCleanupService),
            typeof(TierExpirationCleanupService),
            typeof(DailyStreakResetWorker),
            typeof(StreakWarningWorker),
            typeof(QuotaWarningWorker)
        };

        Assert.Equal(7, hostedServices.Count);
        foreach (var workerType in expectedWorkerTypes)
        {
            Assert.Single(hostedServices, descriptor => descriptor.ImplementationType == workerType);
        }
    }
}
