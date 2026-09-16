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
| `ACME_EMAIL` | `dev@circletools.ch` |
| `IMAGE_TAG` | `latest` (or a commit SHA to pin) |

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

> **TLS is automatic (HTTP-01).** Nothing proxies the VPS, so once the A records resolve, Caddy obtains
> Let's Encrypt certs for all four names via the default HTTP-01 challenge on first boot — no token, no
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

# If the GHCR packages are still private, log in first (PAT with read:packages):
#   echo <ghcr-pat> | docker login ghcr.io -u <your-github-username> --password-stdin

export IMAGE_TAG=latest          # or the commit SHA the workflow built
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

Add these repo secrets — [browser] (Settings → Secrets and variables → Actions):

| Secret | Value |
|---|---|
| `DEPLOY_HOST` | `<server-ip>` |
| `DEPLOY_USER` | `deploy` |
| `DEPLOY_SSH_KEY` | the private key printed above |
| `DEPLOY_KNOWN_HOSTS` | output of `ssh-keyscan <server-ip>` |

Then flip `.github/workflows/deploy.yml` from `workflow_dispatch` to `push: [main]` when you're
ready for automatic deploys (per ADR-0038, milestone 8).

---

## 13. Next (not blocking first deploy)

- **Nightly backups:** `pg_dump` → Infomaniak Swiss Backup, with a **tested restore**.
- **Content-Security-Policy:** tighten the `Caddyfile` (baseline security headers ship already).
- **Durable secrets store** and a **production observability sink** — open sub-decisions in
  [web-api-db.md](web-api-db.md).
