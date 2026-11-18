using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NotificationService.Api;
using NotificationService.Api.Hubs.Readings;
using NotificationService.Application.LiveReadings;
using Shouldly;
using Xunit;

namespace NotificationService.UnitTests.Api;

public sealed class DependencyInjectionTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider = new ServiceCollection()
        .AddLogging()
        .AddReadingsHub()
        .BuildServiceProvider();

    [Fact]
    public void AddReadingsHub_Registered_LimitsClientMessagesTo4Kilobytes()
    {
        // Act
        // Hub-specific options are unset; SignalR falls back to these global ones.
        var options = _serviceProvider.GetRequiredService<IOptions<HubOptions>>().Value;

        // Assert
        options.MaximumReceiveMessageSize.ShouldBe(4 * 1024);
    }

    [Fact]
    public void AddReadingsHub_Registered_NotifiesThroughSignalR()
    {
        // Act
        var notifier = _serviceProvider.GetRequiredService<IReadingsNotifier>();

        // Assert
        notifier.ShouldBeOfType<SignalRReadingsNotifier>();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }
}
