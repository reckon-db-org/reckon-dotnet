using Reckon.Subscriptions;

namespace Reckon.Extensions.Hosting;

/// <summary>
/// Handles events delivered by a <see cref="ReckonSubscriptionService{THandler}"/>.
/// Resolved from a fresh DI scope per event, so it may depend on scoped services
/// (a Marten session, an EF context, a unit of work).
/// </summary>
public interface IReckonEventHandler
{
    /// <summary>
    /// Process one delivered event. Throw to fail the subscription loop (it will
    /// reconnect with backoff and, for a persistent subscription, redeliver the
    /// unacked event); return normally to have the event acknowledged.
    /// </summary>
    Task HandleAsync(SubscriptionEnvelope envelope, CancellationToken cancellationToken);
}
