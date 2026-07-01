#!/usr/bin/env bash
# Stop the local reckon-gateway. Pass -v to also delete the data volume.
set -euo pipefail

here="$(cd -- "$(dirname -- "$0")" && pwd)"
docker compose -f "${here}/docker-compose.yml" down "$@"
