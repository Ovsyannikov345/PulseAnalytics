#!/usr/bin/env bash
#
# Mock deployment of one service to Azure Container Apps.
#
# Simulates what a real rollout would do after CI has pushed a new image:
# ask the platform to pull the image, wait for the new revision, move traffic to it.
# Nothing outside this machine is contacted: the az calls are only printed and the
# platform's answers are made up. Every simulated line is prefixed with [mock].
#
# Usage: deploy/deploy.sh <service> <image>
#   service  data-ingestor | data-processor | api-gateway | notification-service | frontend
#   image    full image reference, e.g. docker.io/ilyaovsyannikov/pulse-analytics-data-ingestor:sha-cb9f9be
#
# Environment (optional):
#   DEPLOY_POLL_COUNT    provisioning polls before the revision is ready (default 3)
#   DEPLOY_POLL_SECONDS  seconds between polls (default 2; 0 for an instant run)
#   GITHUB_STEP_SUMMARY  set by GitHub Actions; a summary table is appended to it

set -euo pipefail

readonly RESOURCE_GROUP="rg-pulse-analytics"
readonly ENVIRONMENT_NAME="cae-pulse-analytics"
readonly SERVICES=(data-ingestor data-processor api-gateway notification-service frontend)

usage() {
  echo "Usage: $0 <service> <image>" >&2
  echo "  service: ${SERVICES[*]}" >&2
  exit 1
}

log() {
  printf '%s %s\n' "$(date -u +%H:%M:%S)" "$*"
}

mock() {
  log "[mock] $*"
}

is_known_service() {
  local candidate
  for candidate in "${SERVICES[@]}"; do
    [[ "$candidate" == "$1" ]] && return 0
  done
  return 1
}

# Revision suffix from the image tag: lowercase letters, digits and dashes only.
revision_suffix() {
  local image="$1" tag="latest"
  if [[ "$image" == *@* ]]; then
    tag="${image##*@}"
    tag="${tag#*:}"
    tag="${tag:0:12}"
  elif [[ "${image##*/}" == *:* ]]; then
    tag="${image##*:}"
  fi
  tag="$(printf '%s' "$tag" | tr '[:upper:]' '[:lower:]' | tr -c 'a-z0-9-' '-')"
  printf '%s' "$tag"
}

[[ $# -eq 2 ]] || usage
service="$1"
image="$2"
is_known_service "$service" || { echo "Unknown service: $service" >&2; usage; }
[[ -n "$image" ]] || { echo "Image must not be empty" >&2; usage; }

poll_count="${DEPLOY_POLL_COUNT:-3}"
poll_seconds="${DEPLOY_POLL_SECONDS:-2}"
app="pulse-${service}"
revision="${app}--$(revision_suffix "$image")"

log "Deploying ${service} to Azure Container Apps (simulated)"
log "  app:      ${app} (resource group ${RESOURCE_GROUP}, environment ${ENVIRONMENT_NAME})"
log "  image:    ${image}"

mock "az login --service-principal --username \$AZURE_CLIENT_ID --tenant \$AZURE_TENANT_ID --federated-token <oidc>"
mock "Logged in"

mock "az containerapp update --name ${app} --resource-group ${RESOURCE_GROUP} --image ${image} --revision-suffix ${revision#"${app}--"}"
mock "Revision ${revision} created; pulling ${image}"

for ((poll = 1; poll <= poll_count; poll++)); do
  sleep "$poll_seconds"
  if ((poll < poll_count)); then
    mock "az containerapp revision show --name ${app} --revision ${revision} -> provisioningState: Provisioning"
  else
    mock "az containerapp revision show --name ${app} --revision ${revision} -> provisioningState: Provisioned, runningState: Running, healthState: Healthy"
  fi
done

mock "az containerapp ingress traffic set --name ${app} --resource-group ${RESOURCE_GROUP} --revision-weight ${revision}=100"
mock "Traffic: 100% -> ${revision}"

log "Deployed ${service}: ${image} is live as revision ${revision}"

if [[ -n "${GITHUB_STEP_SUMMARY:-}" ]]; then
  {
    echo "### Deployed ${service} (mock Azure Container Apps)"
    echo
    echo "| Service | Container app | Image | Revision | Status |"
    echo "|---|---|---|---|---|"
    echo "| ${service} | ${app} | \`${image}\` | ${revision} | Running, 100% traffic |"
  } >> "$GITHUB_STEP_SUMMARY"
fi
