import { describe, expect, it } from 'vitest';
import { blockedIntervals, isPassable, laneClear } from '../../src/game/logic/Occupancy';

const b = (lane: number, z: number, opts: Partial<{ halfL: number; speed: number; jumpable: boolean; lane2: number }> = {}) => ({ lane, z, halfL: 2, speed: 0, jumpable: false, ...opts });

describe('Occupancy', () => {
  it('empty road is passable', () => {
    expect(isPassable([], 20, 5)).toBe(true);
  });

  it('a wall across all three lanes is not passable', () => {
    expect(isPassable([b(0, -60), b(1, -60), b(2, -60)], 20, 6)).toBe(false);
  });

  it('jumpable obstacles never block', () => {
    expect(isPassable([b(0, -60, { jumpable: true }), b(1, -60, { jumpable: true }), b(2, -60, { jumpable: true })], 20, 6)).toBe(true);
  });

  it('two lanes blocked leaves one free lane', () => {
    expect(isPassable([b(0, -60), b(1, -60)], 20, 6)).toBe(true);
  });

  it('requires a feasible path: free lane flips from 0 to 2 instantly with lane 1 blocked throughout', () => {
    // lane 1 blocked by a slow long bus for the whole window; lanes 0 and 2 blocked at overlapping times
    const blockers = [b(1, -20, { halfL: 30 }), b(2, -60, { halfL: 3 }), b(0, -62, { halfL: 3 })];
    expect(isPassable(blockers, 20, 6)).toBe(false);
  });

  it('computes arrival intervals using closing speed', () => {
    const [iv] = blockedIntervals([b(1, -100, { speed: 10, halfL: 2 })], 20, 30);
    // closing speed 10 m/s, reach = 2 + 2.55 → arrives around 9.5 s
    expect(iv.t0).toBeGreaterThan(9);
    expect(iv.t1).toBeLessThan(10.7);
    expect(laneClear([iv], 1, 0, 8)).toBe(true);
    expect(laneClear([iv], 1, 9, 10)).toBe(false);
  });

  it('a lane-changing vehicle blocks both lanes', () => {
    const ivs = blockedIntervals([b(0, -40, { lane2: 1 })], 20, 10);
    expect(ivs.map((i) => i.lane).sort()).toEqual([0, 1]);
  });
});
