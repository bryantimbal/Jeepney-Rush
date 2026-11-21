// AdMob rewarded-ad Server-Side Verification (SSV):
// https://developers.google.com/admob/android/ssv
import { b64Decode } from "./b64.ts";

export const KEYS_URL = "https://www.gstatic.com/admob/reward/verifier-keys.json";
export interface VerifierKey { keyId: number | string; base64: string }

export interface SsvParams {
  message: string;        // the signed part: query string before "&signature="
  signature: Uint8Array;  // DER-encoded ECDSA signature
  keyId: string;
  userId: string | null;
  customData: string | null;
  transactionId: string | null;
  adUnit: string | null;
}

export function parseSsv(url: string): SsvParams | null {
  const q = url.includes("?") ? url.slice(url.indexOf("?") + 1) : url;
  const sigAt = q.indexOf("signature=");
  if (sigAt <= 0) return null;
  const message = q.slice(0, sigAt - 1); // drop the "&" before signature
  const params = new URLSearchParams(q);
  const sig = params.get("signature"), keyId = params.get("key_id");
  if (!sig || !keyId) return null;
  return {
    message, signature: b64Decode(sig), keyId,
    userId: params.get("user_id"), customData: params.get("custom_data"),
    transactionId: params.get("transaction_id"), adUnit: params.get("ad_unit"),
  };
}

/** ASN.1 DER ECDSA signature -> raw r||s (64 bytes) as WebCrypto expects. */
export function derToRaw(der: Uint8Array): Uint8Array {
  if (der[0] !== 0x30) throw new Error("bad_der");
  let i = 2;
  if (der[1] & 0x80) i = 2 + (der[1] & 0x7f);
  const read = () => {
    if (der[i] !== 0x02) throw new Error("bad_der_int");
    const len = der[i + 1];
    let v = der.slice(i + 2, i + 2 + len);
    i += 2 + len;
    while (v.length > 32 && v[0] === 0) v = v.slice(1);
    const out = new Uint8Array(32);
    out.set(v, 32 - v.length);
    return out;
  };
  const r = read(), s = read();
  const raw = new Uint8Array(64);
  raw.set(r, 0);
  raw.set(s, 32);
  return raw;
}

export async function verifySsv(p: SsvParams, keys: VerifierKey[]): Promise<boolean> {
  const k = keys.find((x) => String(x.keyId) === p.keyId);
  if (!k) return false;
  const key = await crypto.subtle.importKey("spki", b64Decode(k.base64) as BufferSource, { name: "ECDSA", namedCurve: "P-256" }, false, ["verify"]);
  try {
    return await crypto.subtle.verify({ name: "ECDSA", hash: "SHA-256" }, key, derToRaw(p.signature) as BufferSource, new TextEncoder().encode(p.message) as BufferSource);
  } catch {
    return false;
  }
}

/**
 * custom_data "kind:ref:nonce" set by the game (e.g. "doublecoins:<run id>:<claim id>").
 * The nonce is the id the game claims the reward with; it is covered by Google's signature.
 */
export function parseCustomData(s: string | null): { kind: string; ref: string | null; nonce: string | null } | null {
  if (!s) return null;
  const parts = s.split(":");
  const kind = parts[0].toLowerCase();
  if (!["revive", "doublecoins", "dailybonus", "trial"].includes(kind)) return null;
  const ref = parts[1] ?? "", nonce = parts[2] ?? "";
  return { kind, ref: ref === "" ? null : ref, nonce: nonce === "" ? null : nonce };
}
