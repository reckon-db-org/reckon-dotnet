# reckon-dotnet

Idiomatic .NET client for the [ReckonDB](https://codeberg.org/reckon-db-org/reckon-db)
event store, accessed over gRPC via the
[reckon-gateway](https://codeberg.org/reckon-db-org/reckon-gateway) frontend.

```csharp
await using var client = await ReckonClient.ConnectAsync("gateway.example.org:50051");
var overview = await client.Health.OverviewAsync();
Console.WriteLine($"node={overview.Node} status={overview.Status}");
```

## What it is

`reckon-dotnet` is the .NET client for the Reckon event-sourcing stack. It
speaks gRPC to a running
[reckon-gateway](https://codeberg.org/reckon-db-org/reckon-gateway), which fronts
a ReckonDB event store (embedded in the gateway, or federated across remote
Erlang clusters). You get typed, idiomatic .NET over the full gateway surface
without speaking Erlang dist or hand-rolling protobuf.

The package id is `Reckon.Client`; the root namespace is `Reckon` (so calls read
`ReckonClient.Connect(...)`). One `ReckonClient` wraps one gRPC channel to one
gateway endpoint; per-service sub-clients are bound to a store and share that
connection.

This is a sibling to [reckon-go](https://codeberg.org/reckon-db-org/reckon-go)
and mirrors its shape 1:1. It does **not** reimplement the
[evoq](https://codeberg.org/reckon-db-org/evoq) framework layer — .NET teams keep
their Wolverine/Marten idioms and back the *store* with ReckonDB through this
client. See the "Reckon and the Critter Stack" appendix in the
[Reckon Codex](https://codeberg.org/reckon-db-org/reckon-codex).

## Sub-clients

| Sub-client | Accessor | Purpose | Status |
|---|---|---|---|
| `health` | `client.Health` | gateway / per-store health | ✅ M0 |
| `streams` | `client.Streams(store)` | append + read + watch events on a stream | ✅ M1 |
| `subscriptions` | `client.Subscriptions(store)` | live + persistent subscriptions | ✅ M2 |
| `snapshots` | `client.Snapshots(store)` | per-stream snapshots | ✅ M2 |
| `dcb` | `client.Dcb(store)` | DCB writes/reads **and CCC payload reads** | ✅ M3 |
| `schema` | `client.Schema(store)` | schema registration + upcasting | ✅ M4 |
| `temporal` | `client.Temporal(store)` | wall-clock / time-travel reads | ✅ M4 |
| `admin` | `client.Admin(store)` | scavenge, links, store stats | ✅ M4 |
| `stores` | `client.Stores` | cluster topology discovery + watch | ✅ M4 |

There is no `Ccc` sub-client by design: CCC (Command Context Consistency) is the
payload-keyed *read* variant of the DCB primitive and reuses DCB's single
conditional-append, so it lives on the `Dcb` sub-client — matching the proto
(`DcbService`), reckon-gater, and reckon-go.

## Dependency injection and hosting

The `Reckon.Extensions.Hosting` package wires the client into a generic host and
gives you a subscription-driven projection worker: ReckonDB owns the log and
delivery, your handler owns the read model.

```csharp
builder.Services.AddReckonClient(o => { o.Address = "gateway:50051"; });

// Drive a projection from a persistent subscription. The handler is resolved
// from a fresh DI scope per event and acked automatically on success.
builder.Services.AddReckonSubscription<OrderProjection>(o =>
{
    o.Store = "orders";
    o.Type = SubscriptionType.EventType;
    o.Selector = "order_placed_v1";
    o.SubscriptionName = "orders-projection";
});

// Optional gateway health check:
builder.Services.AddHealthChecks().AddCheck<ReckonHealthCheck>("reckon");
```

```csharp
public sealed class OrderProjection : IReckonEventHandler
{
    public Task HandleAsync(SubscriptionEnvelope envelope, CancellationToken ct)
    {
        // apply envelope.Event to your read model (Marten doc, EF row, ...)
        return Task.CompletedTask;
    }
}
```

## Wire contract

The gRPC contract is [reckon-proto](https://codeberg.org/reckon-db-org/reckon-proto),
vendored here as a git submodule under `proto/`, pinned to a tag. Stubs are
generated at build time by `Grpc.Tools` into `obj/` (package
`reckon.gateway.v1` → C# namespace `Reckon.Gateway.V1`); nothing generated is
committed.

Clone with submodules:

```bash
git clone --recurse-submodules https://codeberg.org/reckon-db-org/reckon-dotnet
# or, after a plain clone:
git submodule update --init
```

## Build and test

```bash
dotnet build
dotnet test                                   # inert without a lab gateway
RECKON_GATEWAY=beam01.lab:50051 RECKON_INSECURE=1 dotnet test   # live round-trip
```

Need a gateway to point at? [`dev-env/`](dev-env/) spins one up locally in one
command (embedded store `default_store`, gRPC on `localhost:50051`):

```bash
cd dev-env && ./up.sh
RECKON_GATEWAY=localhost:50051 RECKON_INSECURE=1 dotnet test
```

Requires the .NET SDK (repo pins `dotnet 10.0.301` via `.tool-versions`).
Targets `net8.0`, `net9.0` and `net10.0`.

Run the quickstart sample against a lab gateway:

```bash
RECKON_GATEWAY=beam01.lab:50051 RECKON_INSECURE=1 dotnet run --project examples/QuickStart
```

Produce the NuGet packages (`Reckon.Client`, `Reckon.Extensions.Hosting`) with
symbols:

```bash
dotnet pack -c Release --output artifacts
```

## Licence

Apache-2.0. See [LICENSE](LICENSE).
