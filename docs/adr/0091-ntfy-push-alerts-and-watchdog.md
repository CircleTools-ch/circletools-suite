---
status: Accepted
date: 2026-10-31
deciders: [Adrian]
tags: [operations, observability, alerting]
---

# ADR-0091 — Push alerts through a self-hosted ntfy, and a watchdog outside the server

## Context and problem statement

[ADR-0088](0088-mail-capacity-alerting.md) gave Grafana its first alert rules and one channel: email, sent
through the same Brevo relay as the product's mail. Its follow-up names the gap: if Brevo is down or its
daily cap is used up, the alert about exactly that may not arrive. The backup-failure alert (backlog GL-11)
has an optional webhook (`ALERT_WEBHOOK_URL`) but nothing to point it at.

The owner also wants push notifications for their own private monitoring at home, which runs Prometheus and
Grafana of its own. A phone can only receive pushes from a server it can reach from anywhere. The home network
has no public endpoint, but the CircleTools VPS does: Caddy already serves public names with certificates.

A second, related gap: everything that sends alerts runs on the one VPS. If the VPS, Grafana or Prometheus
stops, no alert of any kind is sent, and nothing tells the owner. How should alerts reach the owner's phone
without depending on Brevo, and how does the owner learn that the alerting itself has stopped?

## Decision drivers

- An alert channel that does not share Brevo's failure modes.
- Push to Android and iOS, from CircleTools and from the owner's private senders, with one app on the phone.
- Small: one more container on the VPS is fine, a new paid service is not needed.
- Nothing on the server can report the server's own outage, so that check must run somewhere else.
- No personal data in alerts ([ADR-0012](0012-observability-and-telemetry.md)); credentials stay in
  `deploy/.env` ([ADR-0050](0050-secrets-management.md)); private details stay off git.

## Considered options

1. **Self-hosted ntfy on the CircleTools VPS**, shared by CircleTools and the owner's private senders.
2. **Hosted ntfy.sh** (free with hard-to-guess topic names, or a paid plan with reserved topics).
3. **Self-hosted ntfy on the home network**, reached through a tunnel or VPN.
4. **A chat app's bot** (Telegram, Signal, Slack) as the push channel.

And for the outage check: an **always-firing Grafana watchdog rule that pings healthchecks.io**, versus a
host cron job that pings it.

## Decision outcome

**Chosen: option 1, ntfy on the VPS, plus a Grafana watchdog that pings healthchecks.io.** The VPS is the one
machine that is already public, with TLS, and ntfy costs about 20–30 MB of memory. Hosted ntfy.sh would be
independent of the VPS, but that independence only matters if the VPS is down, and then Grafana is down too;
the watchdog covers that case for both options. Concretely:

- **Service.** `binwiederhier/ntfy` in `deploy/compose.yaml`, static settings in `deploy/ntfy/server.yml`, the
  message cache and user database in the `ntfy_data` volume. Caddy proxies `ntfy.circletools.ch` to it. The
  name comes from `NTFY_HOST`, so the server can run under a non-CircleTools name without a code change.
- **Access is deny-all by default.** Users, access entries and tokens are declared in `deploy/.env`
  (`NTFY_AUTH_USERS`, `NTFY_AUTH_ACCESS`, `NTFY_AUTH_TOKENS`, which ntfy provisions on start). An `owner` admin
  reads every topic on the phone. Each sender is a write-only user limited to its own topic prefix, with one
  token per integration (`circletools` publishes to `circletools-*`; private senders get their own users and
  prefixes). Private topics and senders exist only in the VM's `.env`, never in the repository.
- **Grafana sends every alert two ways.** The contact point `owner` (which replaces ADR-0088's `owner-email`)
  has an email integration and a webhook to `http://ntfy/circletools-alerts?template=grafana`. That URL is on
  the internal Docker network, so the push depends on neither Brevo nor Caddy, DNS or the certificate. ntfy's
  built-in `grafana` template turns the webhook into a readable title and message.
- **Backup failures** reach ntfy through the existing webhook, which now sends `ALERT_WEBHOOK_TOKEN` as a
  Bearer token.
- **iOS** cannot be woken by a self-hosted server. `upstream-base-url: https://ntfy.sh` makes ntfy forward a
  poll request (the message ID only, never its content) to ntfy.sh, which wakes the iPhone app; the app then
  fetches the message from this server.
- **Watchdog.** `deploy/grafana/provisioning/alerting/watchdog.yaml` holds a rule that always fires; a child
  policy routes it only to the `healthchecks` contact point and re-sends it every 5 minutes. healthchecks.io
  alerts the owner by its own email when the pings stop. The rule queries Prometheus (`vector(1)`) and treats
  no data and errors as OK, so the heartbeat stops if the VPS, Grafana, its alerting or Prometheus stops.
- **Required configuration.** `NTFY_AUTH_*`, `NTFY_GRAFANA_TOKEN` and `HEALTHCHECKS_PING_URL` are required by
  `compose.yaml`, as `ALERT_EMAIL_TO` is: Grafana refuses to start with an empty webhook URL, so a missing value
  stops `docker compose` before a deploy pulls or changes anything.

## Consequences

- **Positive:** an alert arrives as a push even when Brevo is the problem; the owner learns of a dead server
  or dead alerting within about 15 minutes; one phone app covers CircleTools and private monitoring; later
  Grafana rules (API down, error rate) need no new channel.
- **Negative:** the owner's private notifications now depend on the CircleTools VPS, and ntfy is one more
  public endpoint to keep patched. Accepted: it is deny-all and holds only short alert texts for 12 hours.
- **Negative:** user and token values appear twice in `.env` (`NTFY_AUTH_TOKENS` and `NTFY_GRAFANA_TOKEN`)
  and must stay in step.
- **Follow-ups:** the owner's home Prometheus can probe `https://api.circletools.ch` from outside as a second
  uptime check (private config, not in this repo). ntfy's data volume needs no backup: the cache expires after
  12 hours and users are provisioned from `.env`.
- **New risks:** a deploy fails until the new values are set in the VM's `.env`, which is loud by design. A
  separate healthchecks.io account is now part of operations; its sign-in belongs in the vault.

## Pros and cons of the options

### Option 1 — Self-hosted ntfy on the VPS (chosen)
- 👍 Already public with TLS; tiny; one server for every sender; messages stay on the Swiss VPS (iOS sends only
  message IDs to ntfy.sh).
- 👎 Shares the VPS's fate; another public endpoint; private use on product infrastructure.

### Option 2 — Hosted ntfy.sh
- 👍 Nothing to run; independent of the VPS.
- 👎 Free topics are protected only by their names; access control needs a paid plan; the messages are stored
  on a US service. Its independence does not remove the need for a watchdog, because Grafana runs on the VPS.

### Option 3 — ntfy on the home network
- 👍 Private notifications don't depend on CircleTools.
- 👎 Needs a tunnel, port forwarding or VPN for the phone and for the VPS to reach it; CircleTools alerts would
  depend on the home connection.

### Option 4 — A chat bot
- 👍 No server; the app is already on the phone.
- 👎 A third-party account and API per channel; alert texts pass through a chat provider; harder to keep
  CircleTools and private senders apart.

### Watchdog: Grafana rule vs host cron job
- 👍 The Grafana rule checks the whole path: VPS, Grafana, its alerting and Prometheus.
- 👎 A cron job would only prove that the host is up, and would be one more host-level unit to install.

## Links

- Related ADRs: [ADR-0088](0088-mail-capacity-alerting.md) (the email channel; this is its follow-up),
  [ADR-0051](0051-production-observability-backend.md) (Grafana stack, alerting upgrade path),
  [ADR-0050](0050-secrets-management.md) (`.env` secrets), [ADR-0063](0063-drop-cloudflare-infomaniak-dns-and-caddy-tls.md)
  (DNS at Infomaniak, Caddy TLS), [ADR-0012](0012-observability-and-telemetry.md) (no personal data in telemetry)
- Runbook: [server setup — push alerts](../deployment/server-setup.md#14-push-alerts-ntfy-and-the-watchdog)
- Backlog: PR-5 (email capacity), PR-7 (alerting to the owner), GL-11 (backup-failure alert)
- arc42 chapters: 07 Deployment view
