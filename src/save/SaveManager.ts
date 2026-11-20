import type { Quality } from '../render/Renderer';

export interface Settings {
  music: boolean;
  sfx: boolean;
  vibration: boolean;
  quality: Quality;
  showFps: boolean;
  tutorialSeen: boolean;
}

export interface RunRecord {
  score: number;
  distance: number;
  delivered: number;
  at: number;
}

export interface SaveData {
  version: 1;
  coins: number;
  bestScore: number;
  bestDistance: number;
  stats: {
    runs: number;
    totalDistance: number;
    totalCoins: number;
    totalDelivered: number;
    totalPickedUp: number;
    crashes: number;
    playTimeSec: number;
  };
  topRuns: RunRecord[];
  settings: Settings;
  createdAt: number;
  updatedAt: number;
}

export interface KeyValueStore {
  get(key: string): string | null;
  set(key: string, value: string): void;
}

const KEY = 'jeepneyRush.save';
const BACKUP = 'jeepneyRush.save.bak';

export function defaultSave(now = Date.now()): SaveData {
  return {
    version: 1,
    coins: 0,
    bestScore: 0,
    bestDistance: 0,
    stats: { runs: 0, totalDistance: 0, totalCoins: 0, totalDelivered: 0, totalPickedUp: 0, crashes: 0, playTimeSec: 0 },
    topRuns: [],
    settings: { music: true, sfx: true, vibration: true, quality: 'medium', showFps: false, tutorialSeen: false },
    createdAt: now,
    updatedAt: now,
  };
}

/** FNV-1a hash: detects truncated/corrupted saves (not a security measure). */
export function checksum(s: string): string {
  let h = 0x811c9dc5;
  for (let i = 0; i < s.length; i++) {
    h ^= s.charCodeAt(i);
    h = Math.imul(h, 0x01000193);
  }
  return (h >>> 0).toString(16);
}

const num = (v: unknown, fallback = 0, max = 1e12): number => (typeof v === 'number' && Number.isFinite(v) && v >= 0 ? Math.min(v, max) : fallback);
const bool = (v: unknown, fallback: boolean): boolean => (typeof v === 'boolean' ? v : fallback);

/** Coerce any parsed object into a valid SaveData, keeping whatever is valid. */
export function sanitize(raw: unknown): SaveData {
  const d = defaultSave();
  if (!raw || typeof raw !== 'object') return d;
  const r = raw as Partial<SaveData> & Record<string, unknown>;
  d.coins = Math.floor(num(r.coins));
  d.bestScore = Math.floor(num(r.bestScore));
  d.bestDistance = num(r.bestDistance);
  const st = (r.stats ?? {}) as Partial<SaveData['stats']>;
  for (const k of Object.keys(d.stats) as (keyof SaveData['stats'])[]) d.stats[k] = num(st[k]);
  const s = (r.settings ?? {}) as Partial<Settings>;
  d.settings.music = bool(s.music, true);
  d.settings.sfx = bool(s.sfx, true);
  d.settings.vibration = bool(s.vibration, true);
  d.settings.showFps = bool(s.showFps, false);
  d.settings.tutorialSeen = bool(s.tutorialSeen, false);
  d.settings.quality = s.quality === 'low' || s.quality === 'medium' || s.quality === 'high' ? s.quality : 'medium';
  if (Array.isArray(r.topRuns)) {
    d.topRuns = r.topRuns
      .filter((x): x is RunRecord => !!x && typeof x === 'object')
      .map((x) => ({ score: Math.floor(num(x.score)), distance: num(x.distance), delivered: Math.floor(num(x.delivered)), at: num(x.at) }))
      .slice(0, 10);
  }
  d.createdAt = num(r.createdAt, Date.now());
  d.updatedAt = num(r.updatedAt, Date.now());
  return d;
}

/**
 * Persistent local save with integrity check and automatic recovery from the
 * last good backup. Storage backend is injectable (localStorage in browser,
 * mirrored to native Preferences on Android, in-memory in tests).
 */
export class SaveManager {
  data: SaveData;
  recovered: 'none' | 'backup' | 'reset' = 'none';

  constructor(private store: KeyValueStore) {
    this.data = this.load();
  }

  private read(key: string): SaveData | null {
    try {
      const raw = this.store.get(key);
      if (!raw) return null;
      const env = JSON.parse(raw) as { sum?: string; body?: string };
      if (typeof env.body !== 'string' || env.sum !== checksum(env.body)) return null;
      return sanitize(JSON.parse(env.body));
    } catch {
      return null;
    }
  }

  load(): SaveData {
    const main = this.read(KEY);
    if (main) return main;
    const bak = this.read(BACKUP);
    if (bak) {
      this.recovered = 'backup';
      return bak;
    }
    let hadData = false;
    try {
      hadData = !!this.store.get(KEY);
    } catch {
      /* ignore */
    }
    if (hadData) this.recovered = 'reset';
    return defaultSave();
  }

  save(): boolean {
    this.data.updatedAt = Date.now();
    const body = JSON.stringify(this.data);
    const env = JSON.stringify({ sum: checksum(body), body });
    try {
      const prev = this.store.get(KEY);
      if (prev) this.store.set(BACKUP, prev);
      this.store.set(KEY, env);
      return true;
    } catch (e) {
      console.warn('[save] failed to persist', e);
      return false;
    }
  }

  /** Record a finished run. Returns whether it set a new best score. */
  recordRun(r: { score: number; distance: number; coins: number; delivered: number; pickedUp: number; crashed: boolean; seconds: number }): { newBest: boolean; newBestDistance: boolean } {
    const d = this.data;
    const score = Math.floor(Math.max(0, r.score));
    const newBest = score > d.bestScore;
    const newBestDistance = r.distance > d.bestDistance;
    if (newBest) d.bestScore = score;
    if (newBestDistance) d.bestDistance = r.distance;
    d.coins += Math.max(0, Math.floor(r.coins));
    d.stats.runs++;
    d.stats.totalDistance += Math.max(0, r.distance);
    d.stats.totalCoins += Math.max(0, Math.floor(r.coins));
    d.stats.totalDelivered += r.delivered;
    d.stats.totalPickedUp += r.pickedUp;
    if (r.crashed) d.stats.crashes++;
    d.stats.playTimeSec += r.seconds;
    d.topRuns.push({ score, distance: Math.round(r.distance), delivered: r.delivered, at: Date.now() });
    d.topRuns.sort((a, b) => b.score - a.score);
    d.topRuns = d.topRuns.slice(0, 10);
    this.save();
    return { newBest, newBestDistance };
  }

  resetProgress(): void {
    const settings = this.data.settings;
    this.data = defaultSave();
    this.data.settings = settings;
    this.save();
  }
}

export class MemoryStore implements KeyValueStore {
  map = new Map<string, string>();
  get(k: string): string | null {
    return this.map.get(k) ?? null;
  }
  set(k: string, v: string): void {
    this.map.set(k, v);
  }
}
