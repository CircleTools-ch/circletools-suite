# Server setup — Infomaniak VPS (first-time provisioning)

Step-by-step provisioning of a **fresh Infomaniak VPS Lite M** (Geneva, Ubuntu 26.04 LTS — or 24.04)
into a hardened Docker host that runs the CircleTools Compose stack. Companion to
[web-api-db.md](web-api-db.md) (the topology & decisions) and [`deploy/`](https://github.com/CircleTools-ch/circletools-suite/tree/main/deploy)
(the stack artifacts). Follow the steps in order.

> **Assumptions & conventions**
> - You are working **as `root`**. Infomaniak's Ubuntu image ships a default **`ubuntu`** user that
>   holds your SSH key; log in as `ssh ubuntu@<server-ip>` then `sudo -i` to become root. (Direct
>   `root` login is blocked by a "login as ubuntu" trap in root's `authorized_keys` — do **not** copy
>   that file anywhere; your real key lives in `/home/ubuntu/.ssh/authorized_keys`.)
> - Commands are labelled **[server]** (run on the VPS), **[laptop]** (run on your machine), or
>   **[browser]** (Infomaniak / GitHub UI).
> - Replace every `<placeholder>` (e.g. `<server-ip>`, `<your-github-username>`).
> - ⚠️ **Do not close your current root session** until Step 5 confirms you can log in as `deploy`.

---

## 1. Update the system — [server]

```bash
apt update && apt full-upgrade -y
apt autoremove -y
```

If the kernel was upgraded, reboot and reconnect:

```bash
[ -f /var/run/reboot-required ] && reboot
```

Optional host identity:

```bash
hostnamectl set-hostname circletools
timedatectl set-timezone Europe/Zurich
```

---

## 2. Create the `deploy` user — [server]

The stack and CI/CD run as a non-root `deploy` user, never as root.

```bash
adduser --disabled-password --gecos "" deploy
usermod -aG sudo deploy

# Copy your REAL public key from the default `ubuntu` user (NOT from root's ~/.ssh, which on
# Infomaniak images is a "login as ubuntu" forced-command trap):
mkdir -p /home/deploy/.ssh
cp /home/ubuntu/.ssh/authorized_keys /home/deploy/.ssh/authorized_keys
chown -R deploy:deploy /home/deploy/.ssh
chmod 700 /home/deploy/.ssh
chmod 600 /home/deploy/.ssh/authorized_keys
```

---

## 3. Firewall (ufw) — [server]

```bash
apt install -y ufw
ufw default deny incoming
ufw default allow outgoing
ufw allow OpenSSH          # 22/tcp
ufw allow 80/tcp           # HTTP (ACME + redirect)
ufw allow 443/tcp          # HTTPS
ufw allow 443/udp          # HTTP/3 (QUIC)
ufw --force enable
ufw status verbose
```

> ⚠️ **ufw does not filter Docker-published ports.** That is fine here because only Caddy publishes
> ports (80/443), and Postgres/API are **never** published (Caddy reaches them on the internal
> Docker network). Never add a `ports:` mapping for `postgres` in `compose.yaml`.

---

## 4. Harden the host — [server]

```bash
# Automatic security updates
apt install -y unattended-upgrades
cat >/etc/apt/apt.conf.d/20auto-upgrades <<'EOF'
APT::Periodic::Update-Package-Lists "1";
APT::Periodic::Unattended-Upgrade "1";
EOF
systemctl enable --now unattended-upgrades

# Ban brute-force SSH attempts (protects sshd out of the box)
apt install -y fail2ban
systemctl enable --now fail2ban
```

---

## 5. Lock down SSH — [server] ⚠️ test before you commit

**First**, from a *separate* terminal, confirm the `deploy` login works:

```bash
# [laptop] — new terminal, keep the root session open!
ssh deploy@<server-ip> "echo ok && sudo -n true 2>/dev/null && echo sudo-ok || echo sudo-needs-password"
```

_(The `sudo -n true` check will report `sudo-needs-password` — that's expected; `deploy` has no
password and needs no sudo. When you need root, use `ssh ubuntu@<server-ip>` then `sudo -i` — this
keeps working even after root login is disabled below.)_

Only once that prints `ok`, disable password auth and root login — [server]:

```bash
cat >/etc/ssh/sshd_config.d/99-circletools.conf <<'EOF'
PasswordAuthentication no
PubkeyAuthentication yes
PermitRootLogin no
EOF
systemctl restart ssh
```

Re-test `ssh deploy@<server-ip>` in a new terminal. From here on, **work as `deploy`**:

```bash
# [laptop]
ssh deploy@<server-ip>
```

---

## 6. Install Docker Engine + Compose — [server, as root]

```bash
curl -fsSL https://get.docker.com | sh
usermod -aG docker deploy
```

Then reconnect **as `deploy`** (so the `docker` group applies) and verify:

```bash
# [laptop] reconnect
ssh deploy@<server-ip>
# [server]
docker --version
docker compose version
docker run --rm hello-world
```

> **On Ubuntu 26.04**, if `get.docker.com` reports no repository for your release codename yet, use
> Ubuntu's own packages instead (Docker 29, pinned for the LTS):
> `sudo apt install -y docker.io docker-compose-v2` — then `sudo usermod -aG docker deploy`.

---

## 7. Place the stack files — [server] + [laptop]

Create the app directory — [server, as root]:

```bash
mkdir -p /opt/circletools
chown deploy:deploy /opt/circletools
```

Copy the Compose stack from your local repo checkout — [laptop] (from the repo root):

```bash
scp deploy/compose.yaml deploy/Caddyfile deploy/.env.example \
    deploy@<server-ip>:/opt/circletools/
```

_(The Dockerfiles are not needed on the box — images are built in CI and pulled from GHCR.)_

---

## 8. Create `.env` with real secrets — [server]

```bash
cd /opt/circletools
cp .env.example .env

# Generate strong random secrets:
echo "POSTGRES_PASSWORD=$(openssl rand -base64 24)"
echo "Auth__Jwt__SigningKey=$(openssl rand -base64 48)"

nano .env
```

Fill in `/opt/circletools/.env` (keys come from `.env.example`):

| Key | Value |
|---|---|
| `POSTGRES_USER` | `circletools` (or your choice) |
| `POSTGRES_PASSWORD` | the generated 24-byte value |
| `ConnectionStrings__maindb` | same user + password: `Host=postgres;Port=5432;Database=maindb;Username=circletools;Password=<same-password>` |
| `Auth__Jwt__SigningKey` | the generated 48-byte value |
| `Mail__Username` / `Mail__Password` | Brevo SMTP login + key |
| `ALERT_EMAIL_TO` | **required** — your address for Grafana's mail-capacity alerts ([ADR-0088](../adr/0088-mail-capacity-alerting.md)) and the backup-failure alert; `docker compose` refuses to start without it |
| `NTFY_AUTH_USERS`, `NTFY_AUTH_ACCESS`, `NTFY_AUTH_TOKENS`, `NTFY_GRAFANA_TOKEN`, `HEALTHCHECKS_PING_URL` | **required** — push alerts and the watchdog ([ADR-0091](../adr/0091-ntfy-push-alerts-and-watchdog.md)); see [step 14](#14-push-alerts-ntfy-and-the-watchdog) |
| `ACME_EMAIL` | `dev@circletools.ch` |
| `IMAGE_TAG` | `edge` for the first bring-up; every deploy then pins the version it built ([container images](container-images.md)) |

Lock the file down:

```bash
chmod 600 /opt/circletools/.env
```

---

## 9. DNS at Infomaniak — [browser]

DNS is managed at Infomaniak ([ADR-0063](../adr/0063-drop-cloudflare-infomaniak-dns-and-caddy-tls.md) —
no Cloudflare). In the Infomaniak Manager, open the `circletools.ch` zone and point these records straight
at the VPS. Get the public IP with — [server]: `hostname -I` or `curl -4 -s https://ifconfig.co`.

| Type | Name | Value |
|---|---|---|
| A | `circletools.ch` (`@`) | `<server-ip>` |
| A | `secretsanta` | `<server-ip>` |
| A | `api` | `<server-ip>` |
| A | `grafana` | `<server-ip>` |
| A | `ntfy` | `<server-ip>` |

> **TLS is automatic (HTTP-01).** Nothing proxies the VPS, so once the A records resolve, Caddy obtains
> Let's Encrypt certs for all five names via the default HTTP-01 challenge on first boot — no token, no
> plugin, no "DNS-only" dance. Just make sure ports **80 and 443** are open (firewall step above) and the
> records have propagated before bringing the stack up.

---

## 10. First deploy — [browser] then [server]

**Build & push the images first** — [browser]: run the **Deploy** workflow in GitHub Actions
(Actions → Deploy → *Run workflow*). Its `build-push` job publishes:

- `ghcr.io/circletools-ch/circletools-api`
- `ghcr.io/circletools-ch/circletools-web`

**Then bring the stack up on the box** — [server]:

```bash
cd /opt/circletools

# The GHCR packages are public (IMG-4, 2026-11-24): no `docker login ghcr.io` is needed. Don't keep one:
# when a stored login expires, GHCR refuses every pull, even of public images (Deploy 1.0.31 failed so).
#   docker logout ghcr.io

export IMAGE_TAG=edge            # or the version / commit SHA the workflow built
docker compose pull
docker compose run --rm migrator # applies EF Core migrations, then exits
docker compose up -d
docker compose ps
```

---

## 11. Verify — [server] + [laptop]

```bash
# [server] — all healthy? migrator should show Exit 0, api/web/postgres "healthy"/"running"
docker compose ps
docker compose logs --tail=50 api

# [laptop] — once DNS + certs are up (grey-cloud during issuance):
curl -sf https://api.circletools.ch/alive        # -> live
```

Then in a **browser**, load each origin over HTTPS with a valid Let's Encrypt cert and a clean
console: `https://circletools.ch`, `https://secretsanta.circletools.ch`, `https://api.circletools.ch`.
Walk a real flow: sign-up / passkey (RP-ID `circletools.ch`), a Brevo email arrives, cross-app SSO
into Secret-Santa works.

---

## 12. Wire CI for future auto-deploys — [server] + [browser]

Create a dedicated CI deploy key so GitHub Actions can SSH in — [server]:

```bash
ssh-keygen -t ed25519 -f ~/deploy_ci -N "" -C "github-actions-deploy"
cat ~/deploy_ci.pub >> ~/.ssh/authorized_keys
cat ~/deploy_ci          # <-- copy this PRIVATE key into the GitHub secret below, then delete it:
# rm ~/deploy_ci ~/deploy_ci.pub
```

Add these as secrets of the **`production` environment** — [browser] (Settings → Environments → `production`;
create it with yourself as required reviewer and deployment branch `main` if it does not exist,
[ADR-0093](../adr/0093-publish-the-repository.md)). Environments need a public repository on GitHub Free.

| Secret | Value |
|---|---|
| `DEPLOY_HOST` | `<server-ip>` |
| `DEPLOY_USER` | `deploy` |
| `DEPLOY_SSH_KEY` | the private key printed above |
| `DEPLOY_KNOWN_HOSTS` | output of `ssh-keyscan <server-ip>` |

Deploys are then started by hand: **Actions → Deploy → Run workflow** on `main`, and the rollout waits for your
approval of the `production` environment. The run refuses a commit whose CI has not succeeded; give it an existing `image_tag` to roll back ([ADR-0047](../adr/0047-versioning-and-release-strategy.md)
amendment 2026-10-28).

---

## 13. Next (not blocking first deploy)

- **Nightly backups:** `pg_dump` → Infomaniak Swiss Backup, with a **tested restore**.
- **Content-Security-Policy:** tighten the `Caddyfile` (baseline security headers ship already).
- **Durable secrets store** and a **production observability sink** — open sub-decisions in
  [web-api-db.md](web-api-db.md).

---

## 14. Push alerts (ntfy) and the watchdog

Grafana sends every alert by email **and** as a push through the self-hosted ntfy, and an always-firing
watchdog rule pings healthchecks.io, which tells you when the pings stop
([ADR-0091](../adr/0091-ntfy-push-alerts-and-watchdog.md)). `docker compose` refuses to start until the values
below are in `.env`, so do this **before the next deploy**.

**1. DNS** — [browser]: add the `ntfy` A record from [step 9](#9-dns-at-infomaniak--browser).

**2. healthchecks.io** — [browser]: create a free account and a check named *CircleTools watchdog* with
**period 5 minutes, grace 10 minutes**. Keep its notification on **email** (healthchecks.io sends it itself,
so it does not depend on Brevo or this server). Copy the ping URL (`https://hc-ping.com/<uuid>`). Put the
account login in the vault.

**3. ntfy users and tokens** — [laptop] (any machine with Docker):

```bash
# Password hashes (asks for the password twice). One for "owner" (the password you log in with on the
# phone), one for "circletools" (a long random password you will never need — it only uses tokens):
docker run --rm -it binwiederhier/ntfy:v2.32.0 user hash
docker run --rm -it binwiederhier/ntfy:v2.32.0 user hash
# Two tokens: one for Grafana, one for the backup alert.
docker run --rm binwiederhier/ntfy:v2.32.0 token generate
docker run --rm binwiederhier/ntfy:v2.32.0 token generate
```

**4. `.env`** — [server]: fill these in `/opt/circletools/.env` (the format is in `.env.example`). The three
`NTFY_AUTH_*` lines **must be in single quotes**, because the hashes contain dollar signs:

```bash
NTFY_AUTH_USERS='owner:<owner-hash>:admin,circletools:<circletools-hash>:user'
NTFY_AUTH_ACCESS='circletools:circletools-*:wo'
NTFY_AUTH_TOKENS='circletools:<grafana-token>:grafana,circletools:<backup-token>:backup'
NTFY_GRAFANA_TOKEN=<grafana-token>
HEALTHCHECKS_PING_URL=https://hc-ping.com/<uuid>
# The backup-failure alert (notify-failure.sh) publishes to the same topic:
ALERT_WEBHOOK_URL=https://ntfy.circletools.ch/circletools-alerts
ALERT_WEBHOOK_TOKEN=<backup-token>
```

Record the hashes' passwords and both tokens in the vault. **Private senders** (for example your home
Grafana) get their own user, prefix and token in the same three lines, such as `home:<hash>:user`,
`home:home-*:wo` and `home:<token>:home-grafana`. Those stay in this `.env` only, never in the repository.

**5. Deploy**, then verify — [server]:

```bash
docker compose ps ntfy                       # "healthy"
curl -s -o /dev/null -w '%{http_code}\n' -d test https://ntfy.circletools.ch/circletools-alerts   # 403
curl -s -H "Authorization: Bearer <backup-token>" -d "Test from the server" \
     https://ntfy.circletools.ch/circletools-alerts                                                # 200
sudo systemctl start circletools-backup-failure@test.service   # the backup alert arrives as a push
```

**6. Phone** — install the ntfy app (Android: Play Store or F-Droid; iOS: App Store). In its settings, set
the default server to `https://ntfy.circletools.ch`, add the user `owner` with its password, and subscribe to
`circletools-alerts` (and your private topics). On Android, allow notifications and disable battery
optimisation for the app; iOS gets instant delivery through the ntfy.sh relay set in `server.yml`.

**7. Watchdog** — [browser]: within ten minutes of the deploy, healthchecks.io shows the check as **up**.
In Grafana, *Alerting → Alert rules* lists the *Watchdog* rule as firing; that is its normal state. The
other rules (mail capacity, *API down*, *API error rate above 5 %*) are normal when they are not firing.

> **Rotating a token:** replace it in `NTFY_AUTH_TOKENS` (and `NTFY_GRAFANA_TOKEN` or `ALERT_WEBHOOK_TOKEN`),
> then `docker compose up -d ntfy grafana`. ntfy removes tokens that are no longer listed.
