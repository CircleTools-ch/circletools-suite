-- SPDX-FileCopyrightText: 2026 CircleTools contributors
-- SPDX-License-Identifier: Apache-2.0

-- Operator read-only database role (ADR-0075).
--
-- Creates (or updates) a login role `circletools_readonly` that may SELECT every table in `maindb` —
-- including tables future migrations add — and nothing else: no INSERT/UPDATE/DELETE, no DDL, not a
-- superuser. It is the role an operator connects with to inspect production data over an SSH tunnel
-- (see docs/deployment/operator-db-read-access.md). The Postgres container publishes no port, so this
-- role is only reachable from inside the VM / the SSH tunnel (ADR-0036 preserved).
--
-- Idempotent — safe to re-run (re-running also ROTATES the password). Run it as the role that owns the
-- schemas (the application / migrator user, i.e. POSTGRES_USER), connected to the application database,
-- supplying the password from the environment so it is NEVER written to this file or to git (ADR-0050):
--
--   psql "$ConnectionStrings__maindb" \
--        -v readonly_password="$CIRCLETOOLS_READONLY_PASSWORD" \
--        -f readonly-role.sql
--
-- To revoke access later: `REVOKE pg_read_all_data FROM circletools_readonly;` (keeps the role) or
-- `DROP OWNED BY circletools_readonly; DROP ROLE circletools_readonly;` (removes it entirely).

\set ON_ERROR_STOP on

-- Create the role once; later runs leave it in place (and only rotate the password, below).
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'circletools_readonly') THEN
    CREATE ROLE circletools_readonly LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION;
  END IF;
END
$$;

-- Set / rotate the password from the psql variable (supplied on the command line, never stored here).
ALTER ROLE circletools_readonly WITH PASSWORD :'readonly_password';

-- May connect only to the current (application) database.
DO $$
BEGIN
  EXECUTE format('GRANT CONNECT ON DATABASE %I TO circletools_readonly', current_database());
END
$$;

-- Read everything, present and future. pg_read_all_data (PostgreSQL 14+) grants USAGE on every schema
-- and SELECT on every table, and automatically covers tables created by later migrations — so this never
-- needs re-running when a new migration adds a table. The role INHERITs, so these privileges apply
-- without an explicit SET ROLE.
--
-- NOTE (ADR-0020): this is deliberately broad because the sole grantee is the trusted operator. It can
-- read the plaintext giver->recipient pairings and PII. For routine "how much data" questions use the
-- FEAT-022 Grafana board instead; for a future, less-trusted grantee, replace this with per-schema
-- SELECT on masked views (ADR-0075 follow-up) rather than pg_read_all_data.
GRANT pg_read_all_data TO circletools_readonly;
