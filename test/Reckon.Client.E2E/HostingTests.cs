using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Reckon;
using Reckon.Extensions.Hosting;
using Reckon.Subscriptions;
using Xunit;

namespace Reckon.Client.E2E;

/// <summary>Unit tests for the DI/hosting wiring. No gateway required.</summary>
public sealed class HostingTests
{
    private sealed class NoopHandler : IReckonEventHandler
    {
        public Task HandleAsync(SubscriptionEnvelope envelope, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Fact]
    public void AddReckonClient_resolves_a_singleton()
    {
        var services = new ServiceCollection();
        services.AddReckonClient(o => { o.Address = "localhost:50051"; o.Insecure = true; });

        using var provider = services.BuildServiceProvider();
        var a = provider.GetRequiredService<ReckonClient>();
        var b = provider.GetRequiredService<ReckonClient>();

        Assert.Same(a, b);
    }

    [Fact]
    public void AddReckonClient_without_address_throws_on_resolve()
    {
        var services = new ServiceCollection();
        services.AddReckonClient(_ => { });

        using var provider = services.BuildServiceProvider();
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<ReckonClient>());
    }

    [Fact]
    public void AddReckonSubscription_validates_options_at_registration() =>
        Assert.Throws<ArgumentException>(() =>
            new ServiceCollection().AddReckonSubscription<NoopHandler>(o => o.Selector = "order_placed_v1"));

    [Fact]
    public void AddReckonSubscription_registers_a_hosted_service()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddReckonClient(o => { o.Address = "localhost:50051"; o.Insecure = true; });
        services.AddReckonSubscription<NoopHandler>(o =>
        {
            o.Store = "orders";
            o.Type = SubscriptionType.EventType;
            o.Selector = "order_placed_v1";
            o.SubscriptionName = "orders-projection";
        });

        using var provider = services.BuildServiceProvider();
        var hosted = provider.GetServices<IHostedService>();

        Assert.Contains(hosted, h => h is ReckonSubscriptionService<NoopHandler>);
    }
}
