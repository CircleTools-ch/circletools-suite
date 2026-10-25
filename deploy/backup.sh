#!/usr/bin/env bash
#
# Nightly Postgres backup -> Infomaniak Swiss Backup (ADR-0036, backlog GL-5).
#
# Streams `pg_dump` (custom/compressed format) straight from the postgres container into an encrypted,
# deduplicated *restic* repository on Swiss Backup's S3 endpoint, then prunes old snapshots and verifies
# repository integrity. The dump never touches the host disk unencrypted, and the offsite copy is
# physically separate from the compute (the single-VM SPOF mitigation ADR-0036 requires).
#
# Tooling note (no ADR — implementation detail of ADR-0036's "nightly pg_dump -> Swiss Backup"):
# restic gives client-side encryption, dedup, retention (`forget --prune`) and an integrity check
# (`restic check`) in one static binary — a good fit for a single maintainer. rclone was the alternative
# but ships no encryption/retention/verify of its own. See docs/deployment/backups.md.
#
# Usage:   ./backup.sh            (installed as a systemd timer on the VM; see backups.md)
#          BACKUP_TAG=predeploy ./backup.sh   (the deploy pipeline, before migrating; kept apart, last 20)
# Config:  read from deploy/.env  (POSTGRES_USER, RESTIC_REPOSITORY, RESTIC_PASSWORD, AWS_* -- template
#          in .env.example). Retention overridable via BACKUP_KEEP_* env vars.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
COMPOSE_FILE="${COMPOSE_FILE:-$SCRIPT_DIR/compose.yaml}"
ENV_FILE="${ENV_FILE:-$SCRIPT_DIR/.env}"

# Retention policy (override in the environment if the volume/cost budget changes).
KEEP_DAILY="${BACKUP_KEEP_DAILY:-7}"
KEEP_WEEKLY="${BACKUP_KEEP_WEEKLY:-5}"
KEEP_MONTHLY="${BACKUP_KEEP_MONTHLY:-6}"
KEEP_PREDEPLOY="${BACKUP_KEEP_PREDEPLOY:-20}"

# "nightly" (the systemd timer) or "predeploy" (the deploy pipeline, right before migrating — ADR-0087).
TAG="${BACKUP_TAG:-nightly}"
case "$TAG" in nightly|predeploy) ;; *) printf 'unknown BACKUP_TAG: %s\n' "$TAG" >&2; exit 1 ;; esac

log() { printf '%s  %s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$*"; }
die() { printf '%s  ERROR: %s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$*" >&2; exit 1; }

[ -f "$ENV_FILE" ] || die "env file not found: $ENV_FILE"
# Export .env so both the compose vars (POSTGRES_*) and the restic vars (RESTIC_*, AWS_*) are in scope
# for restic and `docker compose` below.
set -a
# shellcheck disable=SC1090
. "$ENV_FILE"
set +a

: "${POSTGRES_USER:?POSTGRES_USER missing from $ENV_FILE}"
: "${RESTIC_REPOSITORY:?RESTIC_REPOSITORY missing from $ENV_FILE}"
: "${RESTIC_PASSWORD:?RESTIC_PASSWORD missing from $ENV_FILE}"
command -v restic >/dev/null 2>&1 || die "restic not installed (see docs/deployment/backups.md)"
command -v docker >/dev/null 2>&1 || die "docker not found on PATH"

compose() { docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" "$@"; }

# Initialise the repository on first run (idempotent; a real repo already answers `snapshots`).
if ! restic cat config >/dev/null 2>&1; then
  log "initialising restic repository at $RESTIC_REPOSITORY"
  restic init
fi

log "dumping maindb and streaming into restic"
# -Fc: custom format (compressed, supports selective/parallel restore). Streaming stdout -> restic stdin
# keeps the plaintext dump off the host disk; `set -o pipefail` fails the run if pg_dump errors mid-stream.
compose exec -T postgres pg_dump -U "$POSTGRES_USER" -d maindb -Fc \
  | restic backup --stdin --stdin-filename maindb.dump --tag "$TAG" --host circletools

# Each tag has its own retention, so a pre-deploy snapshot is never pruned by the nightly policy (and the
# nightly run never replaces the last clean state before a deployment — ADR-0087).
if [ "$TAG" = "predeploy" ]; then
  log "pruning pre-deploy snapshots (keep last=$KEEP_PREDEPLOY)"
  restic forget --host circletools --tag predeploy --keep-last "$KEEP_PREDEPLOY" --prune
else
  log "pruning snapshots (keep daily=$KEEP_DAILY weekly=$KEEP_WEEKLY monthly=$KEEP_MONTHLY)"
  restic forget --host circletools --tag "$TAG" \
    --keep-daily "$KEEP_DAILY" --keep-weekly "$KEEP_WEEKLY" --keep-monthly "$KEEP_MONTHLY" \
    --prune
fi

log "verifying repository integrity"
restic check

log "backup complete"
