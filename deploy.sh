#!/usr/bin/env bash
#
# Build, push, and (re)deploy hikidashi to Scaleway Serverless Containers.
# Idempotent: creates the registry/container namespaces and the container on first run,
# updates them on subsequent runs.
#
# Prereqs: docker, scw (Scaleway CLI — run `scw init` once), jq.
#
# Required env vars:
#   SCW_SECRET_KEY   Scaleway API secret key (used for registry docker login)
#   DATABASE_URL     Npgsql connection string (stored as a container SECRET)
#   AUTH_AUTHORITY   WorkOS AuthKit domain, e.g. https://hikidashi-12345.authkit.app
#   AUTH_AUDIENCE    Your https://<endpoint>/mcp URL.
#                    First run: set any placeholder; after you learn the endpoint, set the real
#                    value (and the matching WorkOS Resource Indicator) and re-run. See SETUP.md Part D.
#
# Optional (defaults shown):
#   SCW_REGION=fr-par  REGISTRY_NS=hikidashi  CONTAINER_NS=hikidashi
#   CONTAINER_NAME=hikidashi  IMAGE_TAG=latest  MEMORY_LIMIT=512  CPU_LIMIT=560
#
# NOTE: flag names follow the Scaleway CLI container commands; if Scaleway changes them, check
# `scw container container create --help`. Not yet run against a live account — verify the first run.

set -euo pipefail

SCW_REGION="${SCW_REGION:-fr-par}"
REGISTRY_NS="${REGISTRY_NS:-hikidashi}"
CONTAINER_NS="${CONTAINER_NS:-hikidashi}"
CONTAINER_NAME="${CONTAINER_NAME:-hikidashi}"
IMAGE_TAG="${IMAGE_TAG:-latest}"
MEMORY_LIMIT="${MEMORY_LIMIT:-512}"
CPU_LIMIT="${CPU_LIMIT:-560}"
IMAGE="rg.${SCW_REGION}.scw.cloud/${REGISTRY_NS}/hikidashi:${IMAGE_TAG}"

# --- preflight ---
for bin in docker scw jq; do
  command -v "$bin" >/dev/null 2>&1 || { echo "error: '$bin' not found in PATH" >&2; exit 1; }
done
: "${SCW_SECRET_KEY:?set SCW_SECRET_KEY}"
: "${DATABASE_URL:?set DATABASE_URL}"
: "${AUTH_AUTHORITY:?set AUTH_AUTHORITY}"
: "${AUTH_AUDIENCE:?set AUTH_AUDIENCE (use a placeholder on the very first run)}"

cd "$(dirname "$0")"

echo "==> Ensuring container registry namespace '${REGISTRY_NS}'"
reg_id="$(scw registry namespace list name="$REGISTRY_NS" region="$SCW_REGION" -o json | jq -r '.[0].id // empty')"
if [ -z "$reg_id" ]; then
  scw registry namespace create name="$REGISTRY_NS" region="$SCW_REGION" >/dev/null
  echo "    created."
fi

echo "==> Building and pushing image: ${IMAGE}"
echo "$SCW_SECRET_KEY" | docker login "rg.${SCW_REGION}.scw.cloud" -u nologin --password-stdin
docker build -t "$IMAGE" .
docker push "$IMAGE"

echo "==> Ensuring serverless container namespace '${CONTAINER_NS}'"
ns_id="$(scw container namespace list name="$CONTAINER_NS" region="$SCW_REGION" -o json | jq -r '.[0].id // empty')"
if [ -z "$ns_id" ]; then
  ns_id="$(scw container namespace create name="$CONTAINER_NS" region="$SCW_REGION" -o json | jq -r '.id')"
  echo "    created: ${ns_id}"
fi

# Config applied on both create and update (declarative).
args=(
  registry-image="$IMAGE"
  port=8080
  min-scale=0
  max-scale=1
  memory-limit="$MEMORY_LIMIT"
  cpu-limit="$CPU_LIMIT"
  environment-variables.Auth__Authority="$AUTH_AUTHORITY"
  environment-variables.Auth__Audience="$AUTH_AUDIENCE"
  secret-environment-variables.0.key=DATABASE_URL
  secret-environment-variables.0.value="$DATABASE_URL"
  region="$SCW_REGION"
)

c_id="$(scw container container list namespace-id="$ns_id" name="$CONTAINER_NAME" region="$SCW_REGION" -o json | jq -r '.[0].id // empty')"
if [ -z "$c_id" ]; then
  echo "==> Creating container '${CONTAINER_NAME}' (deploys automatically)"
  c_id="$(scw container container create namespace-id="$ns_id" name="$CONTAINER_NAME" "${args[@]}" -o json | jq -r '.id')"
else
  echo "==> Updating container '${CONTAINER_NAME}' (${c_id}; redeploys automatically)"
  scw container container update "$c_id" "${args[@]}" >/dev/null
fi

domain="$(scw container container get "$c_id" region="$SCW_REGION" -o json | jq -r '.domain_name')"
# Expose the URL to a GitHub Actions step output when running in CI.
if [ -n "${GITHUB_OUTPUT:-}" ]; then
  echo "mcp_url=https://${domain}/mcp" >>"$GITHUB_OUTPUT"
fi
echo
echo "==> Deployed. MCP URL:  https://${domain}/mcp"
if [ "$AUTH_AUDIENCE" != "https://${domain}/mcp" ]; then
  echo "!!  AUTH_AUDIENCE is '${AUTH_AUDIENCE}', but the endpoint is https://${domain}/mcp"
  echo "!!  Set AUTH_AUDIENCE to that URL, add it as a WorkOS Resource Indicator, and re-run this script."
fi
