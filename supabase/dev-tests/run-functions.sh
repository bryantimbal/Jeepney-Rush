#!/usr/bin/env bash
# Type-checks and unit-tests the Supabase Edge Functions with Deno (no network to Google needed).
# Usage: supabase/dev-tests/run-functions.sh   (uses `deno` if installed, else `npx deno@2`)
set -euo pipefail
cd "$(dirname "$0")/../functions"
DENO="deno"; command -v deno >/dev/null || DENO="npx --yes deno@2"
$DENO check --node-modules-dir=none verify-purchase/index.ts admob-ssv/index.ts
$DENO test --node-modules-dir=none tests/
