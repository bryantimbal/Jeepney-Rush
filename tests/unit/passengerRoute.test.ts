import { describe, expect, it } from 'vitest';
import { PassengerRoute } from '../../src/game/logic/PassengerRoute';
import { Random } from '../../src/core/Random';
import { REWARD, SCORE, STOP_NAMES } from '../../src/config';

function route(capacity = 5) {
  return new PassengerRoute(capacity, new Random(42), STOP_NAMES);
}

describe('PassengerRoute', () => {
  it('boards passengers up to capacity and assigns future destinations', () => {
    const r = route(5);
    const a = r.plan(100, -1, 0, 4);
    const b2 = r.plan(400, 1, 2, 4);
    r.plan(700, -1, 0, 2);
    const res = r.serve(a);
    expect(res.boarded).toBe(4);
    expect(res.coins).toBe(4 * REWARD.pickupCoins);
    expect(res.score).toBe(4 * SCORE.pickup);
    expect(r.count).toBe(4);
    for (const d of r.aboard) expect(d).not.toBe(a.id);
    // capacity is respected
    const res2 = r.serve(b2);
    expect(r.count).toBeLessThanOrEqual(5);
    expect(res2.boarded + (4 - res2.alighted)).toBe(r.count);
  });

  it('delivering pays fares with a group bonus and builds a streak', () => {
    const r = route(15);
    const a = r.plan(100, -1, 0, 3);
    const b = r.plan(400, 1, 2, 0);
    // no stops beyond b: everyone must ride to b
    r.serve(a);
    expect(r.count).toBe(3);
    const res = r.serve(b);
    expect(res.alighted).toBe(3);
    expect(res.coins).toBe(3 * REWARD.fareCoins + 2 * REWARD.groupBonusPerPassenger);
    expect(res.streak).toBe(1);
    expect(r.delivered).toBe(3);
    expect(r.count).toBe(0);
  });

  it('missing a stop loses those passengers and resets the streak', () => {
    const r = route(15);
    const a = r.plan(100, -1, 0, 2);
    const b = r.plan(400, 1, 2, 0);
    r.serve(a);
    r.streak = 3;
    const lost = r.miss(b);
    expect(lost).toBe(2);
    expect(r.streak).toBe(0);
    expect(r.missed).toBe(2);
    expect(r.count).toBe(0);
  });

  it('nobody boards when no future stop exists yet', () => {
    const r = route();
    const a = r.plan(100, -1, 0, 3);
    const res = r.serve(a);
    expect(res.boarded).toBe(0);
    expect(res.leftBehind).toBe(3);
  });

  it('never uses the same name for consecutive stops', () => {
    const r = route();
    let prev = '';
    for (let i = 0; i < 50; i++) {
      const s = r.plan(i * 300, -1, 0, 1);
      expect(s.name).not.toBe(prev);
      prev = s.name;
      r.miss(s);
    }
  });
});
