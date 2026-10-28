---
status: Accepted
date: 2026-10-28
deciders: [Adrian]
tags: [operations, observability, email, alerting]
---

# ADR-0088 — Alert before the mail provider's daily cap is reached

## Context and problem statement

All transactional mail goes through the Brevo SMTP relay ([ADR-0037](0037-transactional-email-provider.md)),
whose free tier sends **300 emails per day**. Mail beyond the cap fails; the outbox retries a failed message
up to `Outbox:MaxAttempts` (5) times and then leaves it unsent. A lost verification, reset or member-link mail
is a user who cannot get in or cannot see their match, and nobody would notice until they complain.

December is Secret Santa's peak (backlog PR-5). The estimate is that real use stays far below the cap, but abuse
of the endpoints that accept an email address, or a surprise in uptake, could reach it. How does the owner learn
that the cap is close, early enough to upgrade the Brevo plan?

[ADR-0051](0051-production-observability-backend.md) deferred alerting and listed "Grafana alert rules" as the
upgrade path when it is wanted. It is wanted now.

## Decision drivers

- Warn **before** mail is lost, with time to act (upgrading the plan takes minutes, but someone has to see it).
- Reuse what runs already: Prometheus scrapes the API, Grafana is provisioned from files in git.
- No new service, secret or account.
- No personal data in metrics ([ADR-0012](0012-observability-and-telemetry.md), NFR-004).
- The alert must not depend on the thing it is warning about more than necessary.

## Considered options

1. **Grafana alert rules on a new mail-count gauge** (provisioned from `deploy/grafana/provisioning/alerting/`).
2. **A systemd timer** that queries Postgres and calls `deploy/notify-failure.sh` (as the backup alert does).
3. **Brevo's own notifications / API quota** (account e-mails or polling the Brevo account API).

## Decision outcome

**Chosen: option 1, Grafana alert rules**, because the number lives next to the other business counts, shows
on the dashboard, and needs no new moving part. Concretely:

- **Metrics.** Two gauges on the existing `CircleTools.Business` meter (FEAT-022), refreshed by the same
  background collector, read only from the outbox's type and timestamps — never a payload or address:
  - `circletools.business.mail.sent_last_24h` — mail outbox messages (`mail.*`) with `ProcessedAt` in the
    last **rolling** 24 h. Rolling, so it never depends on where Brevo's day boundary falls; it can only
    over-estimate the provider's count, which is the safe direction.
  - `circletools.business.mail.undeliverable` — mail outbox messages still unsent after `MaxAttempts`.
- **Rules** (`deploy/grafana/provisioning/alerting/mail-capacity.yaml`, evaluated every minute):
  - **warning** when more than **200** mails were sent in 24 h (two-thirds of the cap);
  - **critical** when more than **270** were sent (90 %);
  - **critical** when the undeliverable count **rose in the last hour** — fires once per new failure and
    resolves after an hour, instead of firing forever on old rows (production data is kept, ADR-0087).
  - No data (API down) is **OK**: a down API sends no mail, and uptime is a separate concern.
- **Channel: email** to `ALERT_EMAIL_TO`, sent by Grafana through the same Brevo relay (`GF_SMTP_*` in
  `deploy/compose.yaml`). One reminder every 6 h while a rule stays active.
- `ALERT_EMAIL_TO` is **required** by `compose.yaml`: Grafana refuses to start with an empty contact-point
  address (verified on 13.2.4), so a missing value stops `docker compose` before a deploy pulls or changes
  anything.
- The dashboard gets an **Email** row: the 24 h count with the 200/270 thresholds, and the undeliverable count.

## Consequences

- **Positive:** the owner hears about a cap problem a day's worth of mail before it happens; the volume is
  visible on the dashboard; the alert set is in git and reviewed like code; it is the first alert rule, so
  later rules (error rate, API down) slot into the same file layout and contact point.
- **Negative:** alert mail uses Brevo quota and shares its failure modes — if Brevo is down or the cap was
  already hit, the alert may not arrive. Accepted: the 200 and 270 warnings are sent while quota is left,
  which is exactly when they matter.
- **Negative:** an unrenderable message is marked processed and so counted as sent — a negligible overcount.
- **Follow-ups:** an independent channel (an ntfy webhook contact point) when push alerting is wanted for
  more than this; revisit the thresholds if the Brevo plan changes (they are hard-coded to the 300/day cap).
- **New risks:** a deploy fails until `ALERT_EMAIL_TO` is set in the VM's `.env` — loud by design.

## Pros and cons of the options

### Option 1 — Grafana alert rules (chosen)
- 👍 Uses the running stack; data on the dashboard; provisioned from git; reusable for future alerts.
- 👎 Depends on Prometheus + Grafana being up; alert email shares the Brevo relay.

### Option 2 — systemd timer + `notify-failure.sh`
- 👍 Independent of the observability stack; can use the webhook channel that already exists.
- 👎 A second alerting mechanism with its own SQL and state (when was it last sent?); nothing on the dashboard.

### Option 3 — Brevo's own notifications / API
- 👍 Counts exactly what Brevo counts.
- 👎 Outside the repo: whatever Brevo notifies is not versioned or reviewed with the code, and its timing is
  not ours to set; polling the account API needs an API key (a new secret) and code that only exists for this.

## Links

- Related ADRs: [ADR-0037](0037-transactional-email-provider.md) (Brevo, 300/day),
  [ADR-0051](0051-production-observability-backend.md) (Grafana stack; this takes its alerting upgrade path),
  [ADR-0012](0012-observability-and-telemetry.md) (no PII in telemetry),
  [ADR-0087](0087-production-data-is-kept.md)
- Feature: [FEAT-022](../features/FEAT-022-business-metrics-dashboard.md) / [TP-022](../features/TP-022-business-metrics-dashboard.md)
- Backlog: PR-5 (email capacity for December), PR-7 (alerting to the owner)
- arc42 chapters: 07 Deployment view, 08 Crosscutting concepts
