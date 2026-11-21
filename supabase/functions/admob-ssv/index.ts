// Edge Function: AdMob rewarded-ad server-side verification callback.
// Set this function's URL as the SSV callback of each rewarded ad unit in AdMob.
// Deployed with verify_jwt = false (Google calls it without a user token) – see config.toml.
import { createClient } from "npm:@supabase/supabase-js@2";
import { handleAdmobSsv } from "../_shared/handlers.ts";
import { KEYS_URL, type VerifierKey } from "../_shared/admob_ssv.ts";

const admin = createClient(Deno.env.get("SUPABASE_URL")!, Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!, { auth: { persistSession: false } });
let cache: { at: number; keys: VerifierKey[] } | null = null;

async function keys(): Promise<VerifierKey[]> {
  if (cache && Date.now() - cache.at < 6 * 3600_000) return cache.keys;
  const res = await fetch(KEYS_URL);
  const json = await res.json();
  cache = { at: Date.now(), keys: json.keys };
  return cache.keys;
}

Deno.serve((req) =>
  handleAdmobSsv(req, {
    keys,
    rpc: async (fn, args) => {
      const { data, error } = await admin.rpc(fn, args);
      return { data, error: error ? { message: error.message } : null };
    },
  })
);
