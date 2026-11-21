// Google Play purchase verification (Android Publisher API v3), dependency-free.
import { b64urlEncode, pemToDer } from "./b64.ts";

export interface ServiceAccount { client_email: string; private_key: string; token_uri?: string }
export type FetchFn = (input: string, init?: RequestInit) => Promise<Response>;

const SCOPE = "https://www.googleapis.com/auth/androidpublisher";
const TOKEN_URL = "https://oauth2.googleapis.com/token";

/** Signed JWT assertion for the OAuth2 service-account flow (RS256). */
export async function serviceAccountJwt(sa: ServiceAccount, nowSec = Math.floor(Date.now() / 1000)): Promise<string> {
  const header = b64urlEncode(JSON.stringify({ alg: "RS256", typ: "JWT" }));
  const claims = b64urlEncode(JSON.stringify({ iss: sa.client_email, scope: SCOPE, aud: sa.token_uri ?? TOKEN_URL, iat: nowSec, exp: nowSec + 3600 }));
  const key = await crypto.subtle.importKey("pkcs8", pemToDer(sa.private_key) as BufferSource, { name: "RSASSA-PKCS1-v1_5", hash: "SHA-256" }, false, ["sign"]);
  const sig = new Uint8Array(await crypto.subtle.sign("RSASSA-PKCS1-v1_5", key, new TextEncoder().encode(`${header}.${claims}`) as BufferSource));
  return `${header}.${claims}.${b64urlEncode(sig)}`;
}

export async function accessToken(sa: ServiceAccount, fetchFn: FetchFn): Promise<string> {
  const assertion = await serviceAccountJwt(sa);
  const res = await fetchFn(sa.token_uri ?? TOKEN_URL, {
    method: "POST",
    headers: { "content-type": "application/x-www-form-urlencoded" },
    body: new URLSearchParams({ grant_type: "urn:ietf:params:oauth:grant-type:jwt-bearer", assertion }).toString(),
  });
  if (!res.ok) throw new Error(`google_oauth_${res.status}`);
  const json = await res.json();
  return json.access_token as string;
}

const API = "https://androidpublisher.googleapis.com/androidpublisher/v3/applications";

export async function getProductPurchase(pkg: string, productId: string, token: string, bearer: string, fetchFn: FetchFn) {
  const url = `${API}/${encodeURIComponent(pkg)}/purchases/products/${encodeURIComponent(productId)}/tokens/${encodeURIComponent(token)}`;
  const res = await fetchFn(url, { headers: { authorization: `Bearer ${bearer}` } });
  if (res.status === 404 || res.status === 400 || res.status === 410) return { notFound: true as const };
  if (!res.ok) throw new Error(`google_play_${res.status}`);
  return { notFound: false as const, data: await res.json() as ProductPurchase };
}

export async function acknowledge(pkg: string, productId: string, token: string, bearer: string, fetchFn: FetchFn) {
  const url = `${API}/${encodeURIComponent(pkg)}/purchases/products/${encodeURIComponent(productId)}/tokens/${encodeURIComponent(token)}:acknowledge`;
  const res = await fetchFn(url, { method: "POST", headers: { authorization: `Bearer ${bearer}`, "content-type": "application/json" }, body: "{}" });
  if (!res.ok && res.status !== 409) throw new Error(`google_ack_${res.status}`);
}

/** Subset of https://developers.google.com/android-publisher/api-ref/rest/v3/purchases.products */
export interface ProductPurchase {
  purchaseState?: number;        // 0 purchased, 1 canceled, 2 pending
  consumptionState?: number;     // 0 not consumed, 1 consumed
  acknowledgementState?: number; // 0 not acknowledged, 1 acknowledged
  purchaseType?: number;         // 0 test (license testers), 1 promo, 2 rewarded
  orderId?: string;
  productId?: string;
  obfuscatedExternalAccountId?: string;
}

export type Verdict =
  | { ok: true; orderId: string | null; test: boolean; needsAck: boolean }
  | { ok: false; reason: "not_found" | "pending" | "canceled" | "wrong_product" | "wrong_account" };

/**
 * Decides whether a Play purchase may be granted. A purchase made for another player
 * (obfuscated account id set by the client to its user id) is refused.
 */
export function evaluatePurchase(r: { notFound: boolean; data?: ProductPurchase }, productId: string, userId: string): Verdict {
  if (r.notFound || !r.data) return { ok: false, reason: "not_found" };
  const p = r.data;
  if (p.purchaseState === 2) return { ok: false, reason: "pending" };
  if (p.purchaseState !== 0) return { ok: false, reason: "canceled" };
  if (p.productId && p.productId !== productId) return { ok: false, reason: "wrong_product" };
  if (p.obfuscatedExternalAccountId && p.obfuscatedExternalAccountId !== userId) return { ok: false, reason: "wrong_account" };
  return { ok: true, orderId: p.orderId ?? null, test: p.purchaseType === 0, needsAck: p.acknowledgementState !== 1 };
}
