// Request handlers with injected dependencies (so they can be unit-tested without network).
import { evaluatePurchase, type FetchFn, type ServiceAccount, accessToken, acknowledge, getProductPurchase } from "./google_play.ts";
import { parseCustomData, parseSsv, verifySsv, type VerifierKey } from "./admob_ssv.ts";

export interface Rpc { (fn: string, args: Record<string, unknown>): Promise<{ data: unknown; error: { message: string } | null }> }

const json = (status: number, body: unknown) =>
  new Response(JSON.stringify(body), { status, headers: { "content-type": "application/json", "access-control-allow-origin": "*" } });

export interface PurchaseDeps {
  getUserId: (authHeader: string | null) => Promise<string | null>;
  rpc: Rpc;
  fetchFn: FetchFn;
  serviceAccount: ServiceAccount | null;
  packageName: string | null;
}

/** POST { product_id, purchase_token } with the player's Authorization: Bearer <access token>. */
export async function handleVerifyPurchase(req: Request, d: PurchaseDeps): Promise<Response> {
  if (req.method === "OPTIONS") return json(200, {});
  if (req.method !== "POST") return json(405, { ok: false, reason: "method" });
  const userId = await d.getUserId(req.headers.get("authorization"));
  if (!userId) return json(401, { ok: false, reason: "not_authenticated" });
  let body: { product_id?: string; purchase_token?: string };
  try { body = await req.json(); } catch { return json(400, { ok: false, reason: "bad_request" }); }
  const productId = body.product_id ?? "", token = body.purchase_token ?? "";
  if (!productId || !token || token.length > 4096) return json(400, { ok: false, reason: "bad_request" });

  let orderId: string | null = null, test = false;
  if (token.startsWith("TEST-")) {
    // simulated store in development builds; the database refuses these unless allowed
    test = true;
  } else {
    if (!d.serviceAccount || !d.packageName) return json(500, { ok: false, reason: "server_not_configured" });
    const bearer = await accessToken(d.serviceAccount, d.fetchFn);
    const purchase = await getProductPurchase(d.packageName, productId, token, bearer, d.fetchFn);
    const v = evaluatePurchase(purchase, productId, userId);
    if (!v.ok) return json(200, { ok: false, reason: v.reason });
    orderId = v.orderId;
    test = v.test;
    if (v.needsAck) await acknowledge(d.packageName, productId, token, bearer, d.fetchFn); // otherwise Google refunds after 3 days
  }
  const { data, error } = await d.rpc("jr_srv_grant_purchase", { p_user: userId, p_product: productId, p_token: token, p_order: orderId, p_test: test });
  if (error) return json(500, { ok: false, reason: "db_error", detail: error.message });
  return json(200, data);
}

export interface SsvDeps {
  keys: () => Promise<VerifierKey[]>;
  rpc: Rpc;
}

/** GET callback from AdMob (no user JWT): verify Google's signature, then record the reward. */
export async function handleAdmobSsv(req: Request, d: SsvDeps): Promise<Response> {
  const p = parseSsv(req.url);
  if (!p) return json(400, { ok: false, reason: "bad_request" });
  if (!(await verifySsv(p, await d.keys()))) return json(403, { ok: false, reason: "bad_signature" });
  // AdMob's "verify URL" test sends no user/custom data: acknowledge it so the console check passes
  if (!p.userId || !p.transactionId) return json(200, { ok: true, note: "no user (console test)" });
  const cd = parseCustomData(p.customData);
  if (!cd) return json(200, { ok: false, reason: "unknown_reward" });
  // stored under the game's claim id (signed) – Google's transaction id is the fallback
  const { error } = await d.rpc("jr_srv_record_ad_reward", { p_user: p.userId, p_nonce: cd.nonce ?? p.transactionId, p_kind: cd.kind, p_ref: cd.ref, p_test: false });
  if (error) return json(500, { ok: false, reason: "db_error" });
  return json(200, { ok: true });
}
