import { LANE_COUNT, PLAYER } from '../../config';

/**
 * Lane-occupancy prediction used by the spawn director to guarantee that every
 * generated traffic situation can be survived by a player with good reflexes.
 *
 * World convention: the player sits at z = 0 and the world moves toward +z.
 * An object at z (< 0 ahead) with its own forward speed `speed` approaches at
 * (playerSpeed - speed) m/s.
 */
export interface PredictBlocker {
  lane: number;
  /** Optional second lane the object occupies (e.g. while changing lanes). */
  lane2?: number;
  z: number;
  halfL: number;
  speed: number;
  jumpable: boolean;
}

export interface Interval {
  lane: number;
  t0: number;
  t1: number;
}

const PAD = 0.14; // seconds of safety padding around each blocked interval

export function blockedIntervals(blockers: readonly PredictBlocker[], playerSpeed: number, horizon: number): Interval[] {
  const out: Interval[] = [];
  for (const b of blockers) {
    if (b.jumpable) continue;
    const reach = b.halfL + PLAYER.halfLength;
    const closing = playerSpeed - b.speed;
    let t0: number;
    let t1: number;
    if (closing <= 0.25) {
      // Not approaching: only a problem if it already overlaps the player.
      if (Math.abs(b.z) < reach) {
        t0 = 0;
        t1 = horizon;
      } else continue;
    } else {
      t0 = (-reach - b.z) / closing;
      t1 = (reach - b.z) / closing;
    }
    if (t1 < 0 || t0 > horizon) continue;
    t0 = Math.max(0, t0 - PAD);
    t1 = Math.min(horizon, t1 + PAD);
    out.push({ lane: b.lane, t0, t1 });
    if (b.lane2 !== undefined && b.lane2 !== b.lane) out.push({ lane: b.lane2, t0, t1 });
  }
  return out;
}

/**
 * Returns true if, starting from the lanes in `startLanes` (default: any), a
 * player can weave through all predicted blockers for `horizon` seconds.
 *
 * Physical model on a fine time grid: staying in a lane needs that lane clear;
 * a lane change takes MOVE_TIME during which BOTH lanes must be clear. MOVE_TIME
 * is deliberately longer than the jeepney's real steering time so generated
 * situations leave a human some slack.
 */
export const MOVE_TIME = PLAYER.laneChangeTime + 0.1;
export function isPassable(blockers: readonly PredictBlocker[], playerSpeed: number, horizon: number, step = 0.05, startLanes?: number[]): boolean {
  const intervals = blockedIntervals(blockers, playerSpeed, horizon);
  const steps = Math.ceil(horizon / step);
  const move = Math.max(1, Math.ceil(MOVE_TIME / step));
  const all = (1 << LANE_COUNT) - 1;
  const free = new Array<number>(steps + move).fill(all);
  for (const iv of intervals) {
    const k0 = Math.max(0, Math.floor(iv.t0 / step));
    const k1 = Math.min(steps + move - 1, Math.ceil(iv.t1 / step) - 1);
    for (let k = k0; k <= k1; k++) free[k] &= ~(1 << iv.lane);
  }
  // clearRun[k] = lanes clear for the whole window [k, k+move)
  const clearRun = new Array<number>(steps).fill(0);
  for (let k = 0; k < steps; k++) {
    let m = all;
    for (let j = k; j < k + move; j++) m &= free[j];
    clearRun[k] = m;
  }
  const reach = new Array<number>(steps + move + 1).fill(0);
  if (startLanes?.length) for (const l of startLanes) reach[0] |= 1 << l;
  else reach[0] = all;
  reach[0] &= free[0];
  for (let k = 0; k < steps; k++) {
    const r = reach[k];
    if (!r) return false;
    reach[k + 1] |= r & free[k + 1];
    const cr = clearRun[k];
    for (let l = 0; l < LANE_COUNT; l++) {
      if (!(r & (1 << l)) || !(cr & (1 << l))) continue;
      if (l > 0 && cr & (1 << (l - 1))) reach[k + move] |= 1 << (l - 1);
      if (l < LANE_COUNT - 1 && cr & (1 << (l + 1))) reach[k + move] |= 1 << (l + 1);
    }
  }
  // lanes still "in transit" at the horizon also count as alive
  for (let k = steps; k <= steps + move; k++) if (reach[k]) return true;
  return reach[steps] !== 0;
}

/** Whether a lane is clear of non-jumpable blockers during [t0, t1]. */
export function laneClear(intervals: readonly Interval[], lane: number, t0: number, t1: number): boolean {
  for (const iv of intervals) if (iv.lane === lane && iv.t0 < t1 && iv.t1 > t0) return false;
  return true;
}
