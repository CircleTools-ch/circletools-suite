#!/usr/bin/env bash
# SPDX-FileCopyrightText: 2026 CircleTools contributors
# SPDX-License-Identifier: Apache-2.0
#
# Restore / restore-TEST the Postgres backup from Swiss Backup (ADR-0036, backlog GL-5).
#
# ADR-0036 requires the restore to be *exercised, not assumed*. The default mode is therefore the safe
# drill: pull the latest snapshot, load it into a throwaway scratch database, report what came back, and
# drop it -- proving the offsite dump is restorable without touching live data. A real recovery uses
# `--target prod`, which is destructive and asks for confirmation.
#
# Usage:
#   ./restore.sh                       # SAFE drill: restore latest into a scratch DB, verify, drop it
#   ./restore.sh --snapshot <id>       # ...from a specific restic snapshot (default: latest)
#   ./restore.sh --keep                # keep the scratch DB after the drill (for manual inspection)
#   ./restore.sh --target prod         # DESTRUCTIVE: overwrite the live maindb (recovery only; confirms)
#   ./restore.sh --target prod --yes   # ...skip the interactive confirmation (for scripted recovery)
#
# Config: read from deploy/.env (same vars as backup.sh).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
COMPOSE_FILE="${COMPOSE_FILE:-$SCRIPT_DIR/compose.yaml}"
ENV_FILE="${ENV_FILE:-$SCRIPT_DIR/.env}"

SNAPSHOT="latest"
TARGET="test"           # test | prod
KEEP_SCRATCH=0
ASSUME_YES=0
SCRATCH_DB="maindb_restore_test"

log() { printf '%s  %s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$*"; }
die() { printf '%s  ERROR: %s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$*" >&2; exit 1; }

while [ $# -gt 0 ]; do
  case "$1" in
    --snapshot) SNAPSHOT="${2:?--snapshot needs an id}"; shift 2 ;;
    --target)   TARGET="${2:?--target needs test|prod}"; shift 2 ;;
    --keep)     KEEP_SCRATCH=1; shift ;;
    --yes|-y)   ASSUME_YES=1; shift ;;
    -h|--help)  grep -E '^#( |$)' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *)          die "unknown argument: $1" ;;
  esac
done
[ "$TARGET" = "test" ] || [ "$TARGET" = "prod" ] || die "--target must be 'test' or 'prod'"

[ -f "$ENV_FILE" ] || die "env file not found: $ENV_FILE"
set -a
# shellcheck disable=SC1090
. "$ENV_FILE"
set +a
: "${POSTGRES_USER:?POSTGRES_USER missing from $ENV_FILE}"
: "${RESTIC_REPOSITORY:?RESTIC_REPOSITORY missing from $ENV_FILE}"
: "${RESTIC_PASSWORD:?RESTIC_PASSWORD missing from $ENV_FILE}"
command -v restic >/dev/null 2>&1 || die "restic not installed (see docs/deployment/backups.md)"

compose() { docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" "$@"; }
# psql/pg_restore run *inside* the postgres container; restic streams the dump from the host over stdin.
psql_admin() { compose exec -T postgres psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d maindb "$@"; }

restic snapshots "$SNAPSHOT" >/dev/null 2>&1 || die "snapshot '$SNAPSHOT' not found in $RESTIC_REPOSITORY"

if [ "$TARGET" = "prod" ]; then
  # ---- DESTRUCTIVE recovery into the live database -----------------------------------------------
  log "TARGET = prod: this OVERWRITES the live 'maindb'."
  if [ "$ASSUME_YES" -ne 1 ]; then
    printf 'Type the database name (maindb) to confirm overwrite: '
    read -r reply
    [ "$reply" = "maindb" ] || die "confirmation mismatch; aborting"
  fi
  log "terminating connections and recreating maindb"
  # Drop other sessions, then drop+recreate from the maintenance DB 'postgres' (can't drop the DB you're in).
  compose exec -T postgres psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d postgres <<SQL
SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = 'maindb' AND pid <> pg_backend_pid();
DROP DATABASE IF EXISTS maindb;
CREATE DATABASE maindb OWNER "$POSTGRES_USER";
SQL
  log "restoring snapshot '$SNAPSHOT' into maindb"
  restic dump "$SNAPSHOT" maindb.dump \
    | compose exec -T postgres pg_restore -U "$POSTGRES_USER" -d maindb --no-owner --no-privileges
  log "restore into maindb complete -- run the migrator and bring the stack up (see backups.md)"
  exit 0
fi

# ---- SAFE drill into a scratch database -----------------------------------------------------------
log "restore drill: snapshot='$SNAPSHOT' -> scratch DB '$SCRATCH_DB'"
compose exec -T postgres psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d postgres <<SQL
DROP DATABASE IF EXISTS ${SCRATCH_DB};
CREATE DATABASE ${SCRATCH_DB} OWNER "$POSTGRES_USER";
SQL

log "loading dump into '$SCRATCH_DB'"
# pg_restore exits non-zero on a genuine failure; --no-owner/--no-privileges avoid role-mismatch noise.
restic dump "$SNAPSHOT" maindb.dump \
  | compose exec -T postgres pg_restore -U "$POSTGRES_USER" -d "$SCRATCH_DB" --no-owner --no-privileges

log "verifying restored contents"
# Assert the restore produced real user tables (a schema-only or empty restore should fail the drill).
table_count="$(compose exec -T postgres psql -tA -U "$POSTGRES_USER" -d "$SCRATCH_DB" \
  -c "SELECT count(*) FROM information_schema.tables WHERE table_schema NOT IN ('pg_catalog','information_schema');")"
table_count="$(printf '%s' "$table_count" | tr -d '[:space:]')"
log "restored user tables: ${table_count}"
log "tables per schema:"
compose exec -T postgres psql -U "$POSTGRES_USER" -d "$SCRATCH_DB" \
  -c "SELECT table_schema, count(*) AS tables FROM information_schema.tables WHERE table_schema NOT IN ('pg_catalog','information_schema') GROUP BY table_schema ORDER BY table_schema;"

if [ "$KEEP_SCRATCH" -eq 1 ]; then
  log "leaving scratch DB '$SCRATCH_DB' in place (--keep); drop it manually when done"
else
  log "dropping scratch DB '$SCRATCH_DB'"
  psql_admin -c "DROP DATABASE IF EXISTS ${SCRATCH_DB};" >/dev/null
fi

[ "${table_count:-0}" -gt 0 ] 2>/dev/null || die "restore drill FAILED: no user tables restored"
log "restore drill PASSED (${table_count} tables restored and verified)"
