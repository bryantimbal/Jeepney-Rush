// deno test supabase/functions/tests  – no network needed: keys are generated here and
// Google's APIs are replaced by fakes.
import { assert, assertEquals } from "jsr:@std/assert@1";
import { b64urlEncode, b64Decode } from "../_shared/b64.ts";
import { derToRaw, parseCustomData, parseSsv, verifySsv } from "../_shared/admob_ssv.ts";
import { evaluatePurchase, serviceAccountJwt } from "../_shared/google_play.ts";
import { handleAdmobSsv, handleVerifyPurchase } from "../_shared/handlers.ts";

const enc = new TextEncoder();

function rawToDer(raw: Uint8Array): Uint8Array {
  const int = (b: Uint8Array) => {
    let v = b;
    while (v.length > 1 && v[0] === 0 && !(v[1] & 0x80)) v = v.slice(1);
    if (v[0] & 0x80) v = new Uint8Array([0, ...v]);
    return new Uint8Array([0x02, v.length, ...v]);
  };
  const r = int(raw.slice(0, 32)), s = int(raw.slice(32));
  return new Uint8Array([0x30, r.length + s.length, ...r, ...s]);
}

async function ecKeys() {
  const kp = await crypto.subtle.generateKey({ name: "ECDSA", namedCurve: "P-256" }, true, ["sign", "verify"]);
  const spki = new Uint8Array(await crypto.subtle.exportKey("spki", kp.publicKey));
  let b = ""; for (const x of spki) b += String.fromCharCode(x);
  return { kp, base64: btoa(b) };
}

async function signedSsvUrl(kp: CryptoKeyPair, query: string, keyId = "1234") {
  const raw = new Uint8Array(await crypto.subtle.sign({ name: "ECDSA", hash: "SHA-256" }, kp.privateKey, enc.encode(query) as BufferSource));
  return `https://x.supabase.co/functions/v1/admob-ssv?${query}&signature=${b64urlEncode(rawToDer(raw))}&key_id=${keyId}`;
}

Deno.test("SSV: a correctly signed callback verifies; tampering or a wrong key fails", async () => {
  const { kp, base64 } = await ecKeys();
  const q = "ad_network=5450213213286189855&ad_unit=1234&custom_data=doublecoins%3Arun-1&reward_amount=1&reward_item=Reward&timestamp=1700000000000&transaction_id=tx-1&user_id=11111111-1111-1111-1111-111111111111";
  const url = await signedSsvUrl(kp, q);
  const p = parseSsv(url)!;
  assertEquals(p.message, q);
  assertEquals(p.transactionId, "tx-1");
  assertEquals(p.customData, "doublecoins:run-1");
  assert(await verifySsv(p, [{ keyId: 1234, base64 }]));
  assert(!(await verifySsv({ ...p, message: q.replace("tx-1", "tx-2") }, [{ keyId: 1234, base64 }])), "tampered");
  const other = await ecKeys();
  assert(!(await verifySsv(p, [{ keyId: 1234, base64: other.base64 }])), "wrong key");
  assert(!(await verifySsv(p, [{ keyId: 999, base64 }])), "unknown key id");
});

Deno.test("SSV: DER -> raw conversion handles short and padded integers", () => {
  const raw = new Uint8Array(64); raw[0] = 0x80; raw[31] = 1; raw[32] = 0; raw[33] = 0; raw[63] = 7;
  assertEquals(derToRaw(rawToDer(raw)), raw);
});

Deno.test("SSV: custom data", () => {
  assertEquals(parseCustomData("doublecoins:abc:n1"), { kind: "doublecoins", ref: "abc", nonce: "n1" });
  assertEquals(parseCustomData("dailybonus::n2"), { kind: "dailybonus", ref: null, nonce: "n2" });
  assertEquals(parseCustomData("dailybonus"), { kind: "dailybonus", ref: null, nonce: null });
  assertEquals(parseCustomData("hack:1"), null);
  assertEquals(parseCustomData(null), null);
});

Deno.test("SSV handler records the reward only for valid signatures", async () => {
  const { kp, base64 } = await ecKeys();
  const calls: unknown[] = [];
  const deps = { keys: () => Promise.resolve([{ keyId: "7", base64 }]), rpc: (fn: string, a: Record<string, unknown>) => { calls.push([fn, a]); return Promise.resolve({ data: { ok: true }, error: null }); } };
  const good = await signedSsvUrl(kp, "custom_data=dailybonus%3A%3Aclaim-42&transaction_id=t9&user_id=u1", "7");
  assertEquals((await handleAdmobSsv(new Request(good), deps)).status, 200);
  assertEquals(calls, [["jr_srv_record_ad_reward", { p_user: "u1", p_nonce: "claim-42", p_kind: "dailybonus", p_ref: null, p_test: false }]]);
  const forged = good.replace("user_id=u1", "user_id=u2");
  assertEquals((await handleAdmobSsv(new Request(forged), deps)).status, 403);
  assertEquals(calls.length, 1, "forged request not recorded");
});

Deno.test("Play: purchase evaluation", () => {
  const ok = evaluatePurchase({ notFound: false, data: { purchaseState: 0, orderId: "GPA.1", acknowledgementState: 0, productId: "jr.gems.small" } }, "jr.gems.small", "u1");
  assertEquals(ok, { ok: true, orderId: "GPA.1", test: false, needsAck: true });
  assertEquals(evaluatePurchase({ notFound: true }, "p", "u").ok, false);
  assertEquals(evaluatePurchase({ notFound: false, data: { purchaseState: 2 } }, "p", "u"), { ok: false, reason: "pending" });
  assertEquals(evaluatePurchase({ notFound: false, data: { purchaseState: 1 } }, "p", "u"), { ok: false, reason: "canceled" });
  assertEquals(evaluatePurchase({ notFound: false, data: { purchaseState: 0, productId: "other" } }, "p", "u"), { ok: false, reason: "wrong_product" });
  assertEquals(evaluatePurchase({ notFound: false, data: { purchaseState: 0, obfuscatedExternalAccountId: "someone" } }, "p", "u"), { ok: false, reason: "wrong_account" });
  const tester = evaluatePurchase({ notFound: false, data: { purchaseState: 0, purchaseType: 0, acknowledgementState: 1 } }, "p", "u");
  assertEquals(tester, { ok: true, orderId: null, test: true, needsAck: false });
});

Deno.test("Play: service-account JWT is RS256 and verifiable", async () => {
  const kp = await crypto.subtle.generateKey({ name: "RSASSA-PKCS1-v1_5", modulusLength: 2048, publicExponent: new Uint8Array([1, 0, 1]), hash: "SHA-256" }, true, ["sign", "verify"]);
  const pk = new Uint8Array(await crypto.subtle.exportKey("pkcs8", kp.privateKey));
  let b = ""; for (const x of pk) b += String.fromCharCode(x);
  const pem = `-----BEGIN PRIVATE KEY-----\n${btoa(b)}\n-----END PRIVATE KEY-----\n`;
  const jwt = await serviceAccountJwt({ client_email: "svc@x.iam.gserviceaccount.com", private_key: pem }, 1700000000);
  const [h, c, s] = jwt.split(".");
  assertEquals(JSON.parse(new TextDecoder().decode(b64Decode(h))), { alg: "RS256", typ: "JWT" });
  const claims = JSON.parse(new TextDecoder().decode(b64Decode(c)));
  assertEquals(claims.scope, "https://www.googleapis.com/auth/androidpublisher");
  assertEquals(claims.exp - claims.iat, 3600);
  assert(await crypto.subtle.verify("RSASSA-PKCS1-v1_5", kp.publicKey, b64Decode(s) as BufferSource, enc.encode(`${h}.${c}`) as BufferSource));
});

Deno.test("verify-purchase handler: Google says purchased -> acknowledge -> grant", async () => {
  const kp = await crypto.subtle.generateKey({ name: "RSASSA-PKCS1-v1_5", modulusLength: 2048, publicExponent: new Uint8Array([1, 0, 1]), hash: "SHA-256" }, true, ["sign"]);
  const pk = new Uint8Array(await crypto.subtle.exportKey("pkcs8", kp.privateKey));
  let b = ""; for (const x of pk) b += String.fromCharCode(x);
  const seen: string[] = [];
  const fakeFetch = (url: string) => {
    seen.push(url);
    if (url.includes("oauth2")) return Promise.resolve(new Response(JSON.stringify({ access_token: "at" })));
    if (url.endsWith(":acknowledge")) return Promise.resolve(new Response("{}"));
    return Promise.resolve(new Response(JSON.stringify({ purchaseState: 0, orderId: "GPA.9", acknowledgementState: 0 })));
  };
  const grants: unknown[] = [];
  const deps = {
    getUserId: (h: string | null) => Promise.resolve(h === "Bearer good" ? "user-1" : null),
    rpc: (fn: string, a: Record<string, unknown>) => { grants.push([fn, a]); return Promise.resolve({ data: { ok: true }, error: null }); },
    fetchFn: fakeFetch as never,
    serviceAccount: { client_email: "svc@x", private_key: `-----BEGIN PRIVATE KEY-----\n${btoa(b)}\n-----END PRIVATE KEY-----` },
    packageName: "com.jeepneyrush.game",
  };
  const req = (auth: string, body: unknown) => new Request("https://x/functions/v1/verify-purchase", { method: "POST", headers: { authorization: auth }, body: JSON.stringify(body) });
  assertEquals((await handleVerifyPurchase(req("Bearer bad", {}), deps)).status, 401);
  const res = await handleVerifyPurchase(req("Bearer good", { product_id: "jr.gems.small", purchase_token: "tok-1" }), deps);
  assertEquals(res.status, 200);
  assert(seen.some((u) => u.endsWith(":acknowledge")), "acknowledged so Google won't auto-refund");
  assertEquals(grants, [["jr_srv_grant_purchase", { p_user: "user-1", p_product: "jr.gems.small", p_token: "tok-1", p_order: "GPA.9", p_test: false }]]);
  // test tokens skip Google but are flagged (the database refuses them unless allowed)
  await handleVerifyPurchase(req("Bearer good", { product_id: "jr.gems.small", purchase_token: "TEST-abc" }), deps);
  assertEquals((grants[1] as [string, Record<string, unknown>])[1].p_test, true);
});
