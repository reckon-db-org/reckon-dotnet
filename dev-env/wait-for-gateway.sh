#!/usr/bin/env bash
# Poll the gateway's gRPC port until it accepts a TCP connection, or time out.
# Uses the host's bash /dev/tcp so no in-container tooling is assumed.
set -euo pipefail

host="${1:-localhost}"
port="${2:-50051}"
timeout_secs="${3:-60}"

echo "waiting for ${host}:${port} (timeout ${timeout_secs}s)..."
deadline=$(( SECONDS + timeout_secs ))
until (exec 3<>"/dev/tcp/${host}/${port}") 2>/dev/null; do
  if (( SECONDS >= deadline )); then
    echo "gateway did not come up on ${host}:${port} within ${timeout_secs}s" >&2
    exit 1
  fi
  sleep 1
done
exec 3>&- 2>/dev/null || true
echo "gateway is accepting connections on ${host}:${port}"
