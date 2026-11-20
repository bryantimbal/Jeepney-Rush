import { PLAYER } from '../../config';

export interface Box {
  x: number;
  z: number;
  halfW: number;
  halfL: number;
  /** Top of the object in metres. The player clears it when airborne above this. */
  height: number;
}

/** Forgiveness margins make near-misses feel fair on small phone screens. */
const LATERAL_FORGIVE = 0.18;
const LONG_FORGIVE = 0.25;

export function overlapsPlayer(px: number, py: number, b: Box): boolean {
  return overlapsAt(px, py, b.x, b.z, b);
}

export function overlapsAt(px: number, py: number, x: number, z: number, b: Omit<Box, 'x' | 'z'>): boolean {
  if (Math.abs(px - x) >= PLAYER.halfWidth + b.halfW - LATERAL_FORGIVE) return false;
  if (Math.abs(z) >= PLAYER.halfLength + b.halfL - LONG_FORGIVE) return false;
  return py < b.height - 0.05;
}

/** Generous pickup check for coins/fuel/power-ups (sphere-ish). */
export function collects(px: number, py: number, x: number, y: number, z: number, radius = 1.1): boolean {
  return Math.abs(px - x) < PLAYER.halfWidth + radius * 0.6 && Math.abs(z) < PLAYER.halfLength + radius * 0.5 && Math.abs(py + 0.9 - y) < 2.2;
}

export function laneOfX(x: number, lanes: readonly number[]): number {
  let best = 0;
  let bestD = Infinity;
  for (let i = 0; i < lanes.length; i++) {
    const d = Math.abs(lanes[i] - x);
    if (d < bestD) {
      bestD = d;
      best = i;
    }
  }
  return best;
}
