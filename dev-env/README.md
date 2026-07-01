# dev-env

A one-command local [reckon-gateway](https://codeberg.org/reckon-db-org/reckon-gateway)
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
- Stream ids are validated by the store as `{type}-{id}` (e.g. `user-7c4b9`); a
  plain or multi-hyphen id is rejected as `malformed_user_id`.
- Verified green against gateway `0.16.1`: health, store discovery/stats, and
  the stream append/read/version round-trip. The snapshot, persistent-
  subscription, and DCB E2E paths currently hit server-side crashes in `0.16.1`
  (empty-metadata JSON decode, `subscription_to_proto` clause, DCB emitter
  timeout) — gateway bugs, not client bugs; those tests stay gated and will pass
  against a fixed backing.
