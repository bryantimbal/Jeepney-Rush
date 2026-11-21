// Small base64 helpers shared by the Edge Functions (no dependencies).
export function b64urlEncode(bytes: Uint8Array | string): string {
  const b = typeof bytes === "string" ? new TextEncoder().encode(bytes) : bytes;
  let s = "";
  for (const x of b) s += String.fromCharCode(x);
  return btoa(s).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

export function b64Decode(s: string): Uint8Array {
  const norm = s.replace(/-/g, "+").replace(/_/g, "/").replace(/\s+/g, "");
  const pad = norm.length % 4 === 0 ? norm : norm + "=".repeat(4 - (norm.length % 4));
  const bin = atob(pad);
  const out = new Uint8Array(bin.length);
  for (let i = 0; i < bin.length; i++) out[i] = bin.charCodeAt(i);
  return out;
}

export function pemToDer(pem: string): Uint8Array {
  return b64Decode(pem.replace(/-----(BEGIN|END)[^-]+-----/g, ""));
}
