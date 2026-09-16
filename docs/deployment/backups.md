# Backups & restore (Postgres → Swiss Backup)

> Realises the **nightly `pg_dump` → Infomaniak Swiss Backup, restore tested** requirement of
> [ADR-0036](../adr/0036-cloud-provider-and-hosting.md) and closes backlog item **GL-5**. The scripts
> live in [`deploy/`](https://github.com/CircleTools-ch/circletools-suite/tree/main/deploy)
> (`backup.sh`, `restore.sh`, `systemd/`) and are rsynced to the VM with the rest of the stack.

The single VM is a single point of failure (ADR-0036). The mitigation is an **offsite, encrypted,
nightly** database backup and a **restore that is actually exercised** — a dump you have never restored
is not a backup.

## What it does

`deploy/backup.sh` streams a `pg_dump` of `maindb` straight from the `postgres` container into an
encrypted, deduplicated **restic** repository on Swiss Backup's S3 endpoint, then prunes old snapshots
and verifies repository integrity. The plaintext dump never lands on the host disk.

| Concern | How |
|---|---|
| **Format** | `pg_dump -Fc` (custom/compressed; supports selective + parallel restore). |
| **Offsite** | restic S3 backend → Infomaniak Swiss Backup (physically separate from the compute). |
| **Encryption** | restic encrypts client-side; a Swiss Backup / S3 leak yields only ciphertext. |
| **Retention** | `restic forget --prune`: **7 daily, 5 weekly, 6 monthly** (override via `BACKUP_KEEP_*`). |
| **Integrity** | `restic check` after every run; the run fails loudly if the repo is inconsistent. |
| **Schedule** | systemd timer, nightly 03:30 local, `Persistent=true` (catches a missed run). |

### Tool choice (restic — no ADR)

ADR-0036 fixes the *policy* (nightly `pg_dump` → Swiss Backup, tested restore); the *tool* is an
implementation detail recorded here. **restic** gives client-side encryption, dedup, retention and an
integrity check in one static binary — a good fit for a single maintainer. `rclone` was the alternative
but ships no encryption/retention/verify of its own, and the provider's own snapshotting would keep the
dumps only as long as the account lives. Promote this to an ADR if the backup target or strategy changes
materially.

## Secrets

`backup.sh` / `restore.sh` read [`deploy/.env`](https://github.com/CircleTools-ch/circletools-suite/tree/main/deploy)
(the same file the stack uses). The backup section is in
[`.env.example`](https://github.com/CircleTools-ch/circletools-suite/tree/main/deploy):

| Var | Meaning |
|---|---|
| `RESTIC_REPOSITORY` | `s3:<endpoint>/<bucket>[/<path>]` — the Swiss Backup S3 endpoint + bucket. |
| `RESTIC_PASSWORD` | restic encryption password — **durable, backed-up secret** (losing it strands every backup; keep it in the vault next to the AES/HMAC keys). |
| `AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY` | S3 credentials for the Swiss Backup access. |

Follow the [Secrets & recovery](web-api-db.md#secrets--recovery-adr-0050) policy (ADR-0050): the
authoritative copy lives in the vault; the VM's `.env` is derived from it and never committed.

## How the scripts reach the VM

`backup.sh`, `restore.sh` and `systemd/` are part of the CD rsync in
[`.github/workflows/deploy.yml`](https://github.com/CircleTools-ch/circletools-suite/blob/main/.github/workflows/deploy.yml)
(alongside `compose.yaml` and the observability config), so **every merge to `main` copies them to
`/opt/circletools/`** once the VPS exists (`DEPLOY_HOST` set) — no hand-copy, and a fix to a script ships
with the next deploy. They are invoked via `bash` (the systemd unit and the commands below), so the
missing executable bit after an rsync-from-CI-checkout does not matter.

The two things rsync can't do are **one-time host steps**: installing the systemd units and putting the
backup secrets into `.env` (secrets are never synced).

## One-time setup on the VM

Ubuntu LTS, deploy dir `/opt/circletools` (adjust paths if different). Assumes a deploy has already
landed the scripts (or copy them manually for the very first bring-up before CD is active).

```bash
# 1. Install restic.
sudo apt-get update && sudo apt-get install -y restic

# 2. Create a Swiss Backup S3 access in the Infomaniak dashboard; put the endpoint, bucket and S3
#    credentials into /opt/circletools/.env (see .env.example, "Backups" section). Generate the
#    restic encryption password (NOT the S3 secret key) and store it in the vault:
openssl rand -base64 32     # -> RESTIC_PASSWORD

# 3. First backup by hand — this also initialises the encrypted repo.
cd /opt/circletools && bash ./backup.sh

# 4. Install the nightly timer.
sudo cp /opt/circletools/systemd/circletools-backup.service /etc/systemd/system/
sudo cp /opt/circletools/systemd/circletools-backup.timer   /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now circletools-backup.timer

# 5. Confirm it is scheduled.
systemctl list-timers circletools-backup.timer
```

> If a later deploy changes a **unit file**, re-run step 4's `cp` + `daemon-reload` (only `/opt/…/systemd`
> is refreshed by rsync; the installed copies under `/etc/systemd/system/` are not).

## Testing the restore (the part that matters)

Run the **safe drill** regularly (and after any Postgres major-version bump). It restores the latest
snapshot into a throwaway scratch database, asserts real user tables came back, prints the table count
per schema, and drops the scratch DB — **without touching live data**:

```bash
cd /opt/circletools && bash ./restore.sh
# ... "restore drill PASSED (N tables restored and verified)"
```

Useful variants:

```bash
bash ./restore.sh --snapshot <id>   # drill a specific restic snapshot (list them: restic snapshots)
bash ./restore.sh --keep            # keep the scratch DB afterwards to inspect it manually
```

## Real recovery (destructive)

Part of the [single-VM recovery drill](web-api-db.md#single-vm-recovery-drill). This **overwrites the
live `maindb`** and asks for confirmation:

```bash
docker compose -f compose.yaml --env-file .env stop api        # quiesce writers first
bash ./restore.sh --target prod                                # type "maindb" to confirm
docker compose -f compose.yaml --env-file .env run --rm migrator   # roll forward any pending migrations
docker compose -f compose.yaml --env-file .env up -d
```

## Monitoring

A failed run exits non-zero and shows in `systemctl status circletools-backup` and the journal
(`journalctl -u circletools-backup`). To be alerted, add an `OnFailure=` unit to
`circletools-backup.service`. The restic repository is not scraped by Prometheus today; a future
`restic snapshots` freshness check could feed the Grafana stack ([ADR-0051](../adr/0051-production-observability-backend.md)).

## Status

- ✅ Scripted and committed (`deploy/backup.sh`, `deploy/restore.sh`, `deploy/systemd/`).
- 🔲 **Owner action on the VM:** provision the Swiss Backup access + secrets, install the timer, and
  **run the restore drill** so GL-5's "restore tested" is genuinely exercised, not just written.
