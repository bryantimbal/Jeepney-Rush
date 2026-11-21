// Edge Function: verify a Google Play purchase and grant its contents.
// Secrets: GOOGLE_SERVICE_ACCOUNT_JSON (Play Console API access), PLAY_PACKAGE_NAME.
// SUPABASE_URL / SUPABASE_SERVICE_ROLE_KEY are provided by the Supabase runtime.
import { createClient } from "npm:@supabase/supabase-js@2";
import { handleVerifyPurchase } from "../_shared/handlers.ts";

const admin = createClient(Deno.env.get("SUPABASE_URL")!, Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!, { auth: { persistSession: false } });
const saJson = Deno.env.get("GOOGLE_SERVICE_ACCOUNT_JSON");

Deno.serve((req) =>
  handleVerifyPurchase(req, {
    getUserId: async (auth) => {
      const jwt = auth?.replace(/^Bearer\s+/i, "");
      if (!jwt) return null;
      const { data, error } = await admin.auth.getUser(jwt);
      return error ? null : data.user?.id ?? null;
    },
    rpc: async (fn, args) => {
      const { data, error } = await admin.rpc(fn, args);
      return { data, error: error ? { message: error.message } : null };
    },
    fetchFn: fetch,
    serviceAccount: saJson ? JSON.parse(saJson) : null,
    packageName: Deno.env.get("PLAY_PACKAGE_NAME") ?? null,
  })
);
