#!/usr/bin/env bash
# Start the local reckon-gateway and block until its gRPC port is ready.
set -euo pipefail

here="$(cd -- "$(dirname -- "$0")" && pwd)"

docker compose -f "${here}/docker-compose.yml" up -d
"${here}/wait-for-gateway.sh" localhost 50051 60

cat <<'EOF'

reckon-gateway is up.
  gRPC:     localhost:50051 (plaintext)
  REST/UI:  http://localhost:8080/admin

Run the examples / tests against it:
  RECKON_GATEWAY=localhost:50051 RECKON_INSECURE=1 dotnet test
  RECKON_GATEWAY=localhost:50051 RECKON_INSECURE=1 dotnet run --project examples/QuickStart
EOF
