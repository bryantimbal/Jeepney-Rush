import { describe, expect, it } from 'vitest';
import { SpawnDirector, type SpawnSink } from '../../src/game/logic/SpawnDirector';
import { PassengerRoute, type Stop } from '../../src/game/logic/PassengerRoute';
import { blockedIntervals, type PredictBlocker } from '../../src/game/logic/Occupancy';
import { overlapsAt } from '../../src/game/logic/Collision';
import { HAZARDS, type HazardKind } from '../../src/game/Kinds';
import { Random } from '../../src/core/Random';
import { LANES, PLAYER, SPAWN, STOP_NAMES, baseSpeedAt } from '../../src/config';

interface Obj { kind: HazardKind; lane: number; z: number; speed: number; solid: boolean; hit: boolean }

class MockWorld implements SpawnSink {
  objs: Obj[] = [];
  coins = 0;
  fuel = 0;
  stops: { stop: Stop; z: number }[] = [];
  lanes: number[] = [1];
  playerLanes(): number[] { return this.lanes; }
  blockers(): PredictBlocker[] {
    return this.objs.filter((o) => !o.hit).map((o) => ({ lane: o.lane, z: o.z, halfL: HAZARDS[o.kind].halfL, speed: o.speed, jumpable: HAZARDS[o.kind].jumpable && !o.solid }));
  }
  spawnHazard(kind: HazardKind, lane: number, z: number, speed: number, solid = false): void {
    this.objs.push({ kind, lane, z, speed, solid, hit: false });
  }
  spawnCoins(): void { this.coins++; }
  spawnFuel(): void { this.fuel++; }
  spawnStop(stop: Stop, z: number): void { this.stops.push({ stop, z }); }
}

/**
 * A bot with perfect information but human-like constraints (lane change takes
 * PLAYER.laneChangeTime, a reaction delay, real jump physics) drives through
 * generated traffic. If the generator ever creates an unavoidable situation the
 * bot crashes and the test fails.
 */
function simulate(seed: number, metres: number) {
  const rng = new Random(seed);
  const world = new MockWorld();
  const route = new PassengerRoute(5, rng, STOP_NAMES);
  const director = new SpawnDirector(rng, world, route);
  const dt = 1 / 30;
  let distance = 0;
  let lane = 1, from = 1, laneT = 1, x = LANES[1];
  let y = 0, vy = 0, grounded = true;
  let collisions = 0;
  const hitKinds: string[] = [];
  let thinkTimer = 0;
  while (distance < metres) {
    const speed = baseSpeedAt(distance);
    world.lanes = laneT < 1 ? [from, lane] : [lane];
    director.update(distance, speed);
    for (const o of world.objs) o.z += (speed - o.speed) * dt;
    world.objs = world.objs.filter((o) => o.z < SPAWN.despawnBehind + 6);
    for (const st of world.stops) {
      st.z += speed * dt;
      if (!st.stop.resolved && st.z > 10) route.serve(st.stop);
    }
    distance += speed * dt;

    // ---- bot: plan a lane path through predicted blocked intervals
    thinkTimer -= dt;
    if (laneT >= 1 && thinkTimer <= 0) {
      thinkTimer = 0; // re-plan every frame
      // Fine time grid; a lane change occupies MOVE consecutive steps during
      // which both lanes must be clear – the real steering time.
      const step = 0.05;
      const MOVE = Math.ceil(PLAYER.laneChangeTime / step);
      const solidIvs = blockedIntervals(world.blockers().map((q) => ({ ...q, jumpable: false })), speed, 9.5);
      const softIvs = blockedIntervals(world.blockers(), speed, 9.5);
      let ivs = solidIvs;
      const H = Math.floor(9 / step);
      const plan = () => {
        const freeMask: number[] = [];
        for (let k = 0; k < H + MOVE; k++) {
          let m = 7;
          for (const iv of ivs) if (iv.t0 < (k + 1) * step && iv.t1 > k * step) m &= ~(1 << iv.lane);
          freeMask.push(m);
        }
        const clear = (l: number, k0: number, n: number) => {
          for (let k = k0; k < k0 + n; k++) if (!(freeMask[k] & (1 << l))) return false;
          return true;
        };
        // backward DP: alive[k] = lanes from which you survive from step k to H
        const alive: number[] = new Array(H + MOVE + 1).fill(7);
        for (let k = H - 1; k >= 0; k--) {
          let m = 0;
          for (let l = 0; l < 3; l++) {
            if (clear(l, k, 1) && alive[k + 1] & (1 << l)) { m |= 1 << l; continue; }
            for (const n of [l - 1, l + 1]) if (n >= 0 && n <= 2 && clear(l, k, MOVE) && clear(n, k, MOVE) && alive[k + MOVE] & (1 << n)) m |= 1 << l;
          }
          alive[k] = m;
        }
        if (clear(lane, 0, 1) && alive[1] & (1 << lane)) {
          // staying is safe – but move early if staying only works via a later dodge that is about to expire
          return lane;
        }
        for (const n of [lane - 1, lane + 1]) if (n >= 0 && n <= 2 && clear(lane, 0, MOVE) && clear(n, 0, MOVE) && alive[MOVE] & (1 << n)) return n;
        return -1;
      };
      let best = plan();
      if (best < 0) {
        ivs = softIvs;
        best = plan();
      }
      if (best < 0) best = lane;
      if (best !== lane) { from = lane; lane = best; laneT = 0; }
    }
    if (laneT < 1) laneT = Math.min(1, laneT + dt / PLAYER.laneChangeTime);
    const e = 1 - Math.pow(1 - laneT, 3);
    x = LANES[from] + (LANES[lane] - LANES[from]) * e;

    // ---- bot: jump over jumpables in its lane at the right moment
    if (grounded) {
      for (const o of world.objs) {
        const def = HAZARDS[o.kind];
        if (o.hit || !def.jumpable || o.solid) continue;
        if (Math.abs(LANES[o.lane] - x) > 1.5) continue;
        const gap = -o.z - def.halfL - PLAYER.halfLength;
        if (gap > 0 && gap < speed * 0.16) { vy = PLAYER.jumpVelocity; grounded = false; break; }
      }
    } else {
      vy -= PLAYER.gravity * dt;
      y += vy * dt;
      if (y <= 0) { y = 0; vy = 0; grounded = true; }
    }

    for (const o of world.objs) {
      if (o.hit) continue;
      if (overlapsAt(x, y, LANES[o.lane], o.z, HAZARDS[o.kind])) {
        o.hit = true;
        collisions++;
        hitKinds.push(`${o.kind}@${Math.round(distance)}m`);
      }
    }
  }
  return { collisions, hitKinds, delivered: route.delivered, stops: world.stops.length, coins: world.coins, fuel: world.fuel, rejected: director.rejected };
}

describe('SpawnDirector fairness', () => {
  for (const seed of [1, 2, 3, 4, 5, 6, 7, 8]) {
    it(`seed ${seed}: a skilled bot survives 6 km without collisions`, () => {
      const r = simulate(seed, 6000);
      expect(r.hitKinds).toEqual([]);
      expect(r.stops).toBeGreaterThan(8);
      expect(r.coins).toBeGreaterThan(50);
      expect(r.fuel).toBeGreaterThan(5);
      expect(r.delivered).toBeGreaterThan(10);
    });
  }
});
