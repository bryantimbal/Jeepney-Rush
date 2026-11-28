#!/usr/bin/env bash
# Runs the Supabase migrations and tests against a throw-away local Postgres 15/16.
# Usage: supabase/dev-tests/run.sh      (needs the postgres server binaries; uses port 54329)
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../.." && pwd)"
PGBIN="${PGBIN:-$(ls -d /usr/lib/postgresql/*/bin 2>/dev/null | sort -V | tail -1)}"
DATA="$(mktemp -d /tmp/jr-pg-XXXXXX)"
PORT=54329
RUNAS=""
if [ "$(id -u)" = "0" ]; then RUNAS="runuser -u postgres --"; chown postgres "$DATA"; fi
cleanup() { $RUNAS "$PGBIN/pg_ctl" -D "$DATA" -m immediate stop >/dev/null 2>&1 || true; rm -rf "$DATA"; }
trap cleanup EXIT
$RUNAS "$PGBIN/initdb" -D "$DATA" -U postgres -A trust >/dev/null
$RUNAS "$PGBIN/pg_ctl" -D "$DATA" -o "-p $PORT -k $DATA -c listen_addresses=''" -l "$DATA/log" -w start >/dev/null
export PGOPTIONS="-c client_min_messages=notice"
PSQL=(psql -h "$DATA" -p $PORT -U postgres -X -q -v ON_ERROR_STOP=1)
"${PSQL[@]}" -c "create database jr" >/dev/null
DB=("${PSQL[@]}" -d jr)
PGOPTIONS="-c client_min_messages=warning" "${DB[@]}" -f "$HERE/shim.sql" >/dev/null
for f in $(ls "$ROOT"/supabase/migrations/*.sql | sort); do
  echo "migrate  $(basename "$f")"
  PGOPTIONS="-c client_min_messages=warning" "${DB[@]}" -f "$f" >/dev/null
done
# re-running migrations must be safe
for f in $(ls "$ROOT"/supabase/migrations/*.sql | sort); do PGOPTIONS="-c client_min_messages=warning" "${DB[@]}" -f "$f" >/dev/null; done
echo "migrate  (re-applied, idempotent)"
cd "$HERE"
for t in vectors.sql behaviour.sql monetization.sql account_deletion.sql road_gems.sql arcade_combo.sql stat_classes.sql career.sql passenger_missions.sql cosmetics.sql game_modes.sql ghosts.sql app_version.sql barangays.sql daily_ghosts.sql play_test_purchases.sql login_streak.sql christmas_event.sql leagues.sql operator.sql pasada_pass.sql album.sql social.sql; do
  echo "test     $t"
  "${DB[@]}" -f "$t" 2>&1 | sed 's/^psql:[^ ]* NOTICE:  /         /' | grep -v '^\s*$' | grep -v '^[tf]$'
  [ "${PIPESTATUS[0]}" = "0" ] || { echo "FAILED: $t"; exit 1; }
done
echo "ALL SUPABASE TESTS PASSED"
