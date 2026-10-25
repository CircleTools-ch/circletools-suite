# Operator read-only database access

This runbook sets up and uses **read-only** access to the production PostgreSQL database for inspecting
what data exists (debugging a support question, confirming a migration landed, sanity-checking a release).
It implements [ADR-0075](../adr/0075-operator-read-only-database-access.md).

> [!IMPORTANT]
> **Read access exposes the most sensitive data in the product.** By
> [ADR-0020](../adr/0020-server-side-assignment-privacy.md) the server stores draw results
> (giver → recipient pairings) and names in **plaintext**, and PII lives in `AspNetUsers` and transiently
> in `circles.invitations`. A `SELECT` sees all of it.
>
> For routine *"how much data / how fast is it growing?"* questions, **do not use raw access** — use the
> PII-free [business-metrics Grafana board](dev-observability.md) (FEAT-022) at
> `grafana.circletools.ch`. Reserve the steps below for genuine row-level debugging.

## What you get

A dedicated login role **`circletools_readonly`** that may `SELECT` every table in `maindb` — including
tables future migrations add — and **nothing else**: no `INSERT`/`UPDATE`/`DELETE`, no DDL, not a
superuser. The Postgres container **publishes no port** ([ADR-0036](../adr/0036-cloud-provider-and-hosting.md)),
so the role is only reachable from inside the VM or through an SSH tunnel — there is no public database
endpoint.

## One-time setup

1. **Generate a strong password** and store it where the other runtime secrets live — the VM's git-ignored
   `/opt/circletools/.env` **and** your password manager ([ADR-0050](../adr/0050-secrets-management.md)).
   It is never committed. For example add to `.env`:

   ```ini
   CIRCLETOOLS_READONLY_PASSWORD=<generated-strong-password>
   ```

2. **Copy the script to the VM.** `/opt/circletools` holds only `compose.yaml`, `Caddyfile` and `.env`
   (see [server setup](server-setup.md)) — the repo is **not** checked out there — so send the one file
   over from your working copy:

   ```bash
   scp deploy/sql/readonly-role.sql deploy@<server-ip>:/opt/circletools/
   ```

3. **Create the role.** Run the script as the role that owns the schemas (the application / migrator user,
   i.e. `POSTGRES_USER`), connected to `maindb`, passing the password from the environment so it is never
   written to a file. On the VM:

   ```bash
   cd /opt/circletools
   set -a; . ./.env; set +a      # load POSTGRES_USER and CIRCLETOOLS_READONLY_PASSWORD
   docker compose exec -T postgres \
     psql -U "$POSTGRES_USER" -d maindb \
     -v readonly_password="$CIRCLETOOLS_READONLY_PASSWORD" \
     < readonly-role.sql
   ```

   `docker compose exec` connects over the container's local socket, so the `POSTGRES_USER` login needs
   **no password** here. The script is **idempotent** — re-running it is how you **rotate** the password
   later. It uses the predefined `pg_read_all_data` role, so it never needs re-running when a migration
   adds a table.

4. **Verify** the role works and is read-only:

   ```bash
   docker compose exec postgres \
     psql "postgresql://circletools_readonly:$CIRCLETOOLS_READONLY_PASSWORD@localhost/maindb" \
     -c "SELECT count(*) FROM public.\"AspNetUsers\";" \
     -c "SELECT 1 INTO temp_write_probe;"   # must FAIL: permission denied
   ```

   The `SELECT count(*)` returns a number; the write probe must be rejected with *permission denied*.

## Connecting

### Quick look, on the VM (CLI)

```bash
cd /opt/circletools
docker compose exec postgres psql -U circletools_readonly -d maindb
# password: $CIRCLETOOLS_READONLY_PASSWORD
```

### From your laptop with a GUI client (SSH tunnel)

The container has no host-published port, but the VM can reach it on the Docker network, and `ssh -L` can
forward straight to the container's IP — so nothing needs to be exposed. Open the tunnel:

```bash
# Resolve the postgres container's IP on the VM and forward local port 15432 to it.
PG_IP=$(ssh circletools-vm "docker inspect -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' \
  \$(docker compose -f /opt/circletools/compose.yaml ps -q postgres)")
ssh -N -L 15432:${PG_IP}:5432 circletools-vm
```

Then point `psql`, DBeaver or pgAdmin at:

- **Host** `localhost` · **Port** `15432` · **Database** `maindb`
- **User** `circletools_readonly` · **Password** the value from `.env` / your password manager
- **SSL** not required (the hop is already encrypted by SSH; the DB link is loopback→Docker network)

Close the tunnel (Ctrl-C) when you're done.

## Revoking or rotating

- **Rotate the password:** re-run the setup script with a new `CIRCLETOOLS_READONLY_PASSWORD` and update
  `.env` + the password manager.
- **Revoke access, keep the role:** `REVOKE pg_read_all_data FROM circletools_readonly;`
- **Remove entirely:** `DROP OWNED BY circletools_readonly; DROP ROLE circletools_readonly;`

## Notes & future hardening

- This grant is deliberately broad because the sole grantee is the trusted operator (ADR-0020). If a
  **less-trusted** person ever needs access, replace `pg_read_all_data` with per-schema `SELECT` on
  **masked views** that omit the sensitive columns (the `secretsanta.groups` and `secretsanta.member_entries`
  JSON bodies, the `sync.aggregates` JSON bodies, `circles.invitations.email`, and the `AspNetUsers`
  email/hash columns) — the ADR-0075 follow-up. (Before ADR-0086 the list named `linking.assignments` and the
  `shared` schema; both are gone.)
- For a deep dive that must not touch live data at all, restore a backup into a throwaway local database
  (`deploy/restore.sh`) and inspect that instead.
