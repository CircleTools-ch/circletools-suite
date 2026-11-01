#!/usr/bin/env bash
#
# OnFailure alert handler for the nightly backup (backlog GL-11). Invoked by
# deploy/systemd/circletools-backup-failure@.service when circletools-backup.service fails, with the
# failed unit name as $1.
#
# Best-effort and non-fatal by design: it ALWAYS exits 0, so a notification hiccup never cascades into
# another failed unit. It records the alert in the journal regardless, and additionally sends any
# channel configured in deploy/.env (a webhook and/or an email via the Brevo SMTP relay). With no
# channel configured it degrades to the journal line only.
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="${ENV_FILE:-$SCRIPT_DIR/.env}"
UNIT="${1:-circletools-backup.service}"

if [ -f "$ENV_FILE" ]; then
  set -a
  # shellcheck disable=SC1090
  . "$ENV_FILE"
  set +a
fi

HOST="$(hostname -f 2>/dev/null || hostname)"
WHEN="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
LOGTAIL="$(journalctl -u "$UNIT" -n 20 --no-pager -o cat 2>/dev/null || echo '(journal unavailable)')"

SUBJECT="[CircleTools] backup FAILED on ${HOST}"
BODY="The scheduled backup unit '${UNIT}' failed on ${HOST} at ${WHEN} UTC.

Last log lines:
${LOGTAIL}

Investigate:  systemctl status ${UNIT}  ;  journalctl -u ${UNIT}
Runbook:      docs/deployment/backups.md"

# Always leave a trail, even if every channel below is unconfigured or fails.
logger -t circletools-backup "ALERT: ${SUBJECT}" 2>/dev/null || true

# 1) Optional webhook: a plain-text POST that suits ntfy, healthchecks.io, or a chat webhook. The CircleTools
#    ntfy server denies anonymous publishing (ADR-0091), so ALERT_WEBHOOK_TOKEN is sent as a Bearer token.
if [ -n "${ALERT_WEBHOOK_URL:-}" ]; then
  AUTH_HEADER=()
  if [ -n "${ALERT_WEBHOOK_TOKEN:-}" ]; then
    AUTH_HEADER=(-H "Authorization: Bearer ${ALERT_WEBHOOK_TOKEN}")
  fi
  if curl -fsS -m 20 -H "Title: ${SUBJECT}" -H "Priority: high" "${AUTH_HEADER[@]}" \
       --data-binary "${BODY}" "${ALERT_WEBHOOK_URL}" >/dev/null 2>&1; then
    echo "alert: webhook sent"
  else
    echo "alert: webhook FAILED"
  fi
fi

# 2) Optional email via the Brevo SMTP relay (reuses Mail__Username / Mail__Password from .env).
TO="${ALERT_EMAIL_TO:-}"
FROM="${ALERT_EMAIL_FROM:-no-reply@circletools.ch}"
SMTP_URL="${ALERT_SMTP_URL:-smtp://smtp-relay.brevo.com:587}"
if [ -n "$TO" ] && [ -n "${Mail__Username:-}" ] && [ -n "${Mail__Password:-}" ]; then
  if {
        printf 'From: CircleTools Alerts <%s>\r\n' "$FROM"
        printf 'To: %s\r\n' "$TO"
        printf 'Subject: %s\r\n' "$SUBJECT"
        printf 'Content-Type: text/plain; charset=utf-8\r\n\r\n'
        printf '%s\r\n' "$BODY"
      } | curl -sS -m 30 --url "$SMTP_URL" --ssl-reqd \
            --user "${Mail__Username}:${Mail__Password}" \
            --mail-from "$FROM" --mail-rcpt "$TO" --upload-file - >/dev/null 2>&1; then
    echo "alert: email sent to ${TO}"
  else
    echo "alert: email FAILED"
  fi
fi

exit 0
