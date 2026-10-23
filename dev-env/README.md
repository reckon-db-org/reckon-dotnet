# dev-env

A one-command local [reckon-gateway](https://github.com/reckon-db-org/reckon-gateway)
in embedded-store mode, so the examples and the gated E2E tests can run against
a real store.

## Start

```bash
cd dev-env
./up.sh
```

This runs the gateway container and blocks until its gRPC port accepts
connections. It exposes:

| Host port | Protocol |
|---|---|
| `50051` | gRPC (plaintext) |
| `18080` | REST API + admin UI at <http://localhost:18080/admin> |

The store id is **`default_store`**, matching the SDK default, so clients need
only two environment variables.

## Use it

From the repo root:

```bash
# Run the gated E2E tests against the live gateway
RECKON_GATEWAY=localhost:50051 RECKON_INSECURE=1 dotnet test

# Run the quickstart sample
RECKON_GATEWAY=localhost:50051 RECKON_INSECURE=1 dotnet run --project examples/QuickStart
```

## Stop

```bash
./down.sh        # stop, keep the data volume
./down.sh -v     # stop and wipe the store's data
```

## Notes

- The image is pulled from `ghcr.io/reckon-db-org/reckon-gateway`. If your host
  uses Podman, substitute `podman compose` (or `podman-compose`) for
  `docker compose` in the scripts.
- DCB works out of the box; the CCC payload-index E2E path additionally needs a
  store with `{ccc, key}` indexes declared, which the default embedded store
  does not configure.
- Data persists in the `reckon-data` named volume across restarts until you run
  `./down.sh -v`.
- Stream ids are validated by the store as `<prefix>-<32 hex>` (e.g.
  `user-018f6a7b8c9d4e5f60718293a4b5c6d7`); any other shape is rejected as
  `malformed_user_id`. See
  [Stream Ids](https://hexdocs.pm/reckon_gater/stream_ids.html).
- The pinned gateway is 0.30.2. Against it the whole E2E suite passes (health,
  store discovery, stream append/read/version, subscriptions, snapshots, DCB
  commit and conflict), and `examples/QuickStart` runs to the end, DCB loop
  included.
