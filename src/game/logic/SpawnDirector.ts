import { LANES, LANE_COUNT, SPAWN, PLAYER, difficultyAt } from '../../config';
import type { Random } from '../../core/Random';
import { HAZARDS, type HazardKind, type StaticKind, type VehicleKind } from '../Kinds';
import { blockedIntervals, isPassable, laneClear, type PredictBlocker } from './Occupancy';
import type { PassengerRoute, Stop } from './PassengerRoute';

/** What the director needs from the world. Implemented by the game, mocked in tests. */
export interface SpawnSink {
  blockers(): PredictBlocker[];
  /** Lane(s) the player currently occupies (two while changing lanes). */
  playerLanes(): number[];
  /** `solid`: a jumpable object the generator treats as blocking its lane (only jump walls are not solid). */
  spawnHazard(kind: HazardKind, lane: number, z: number, speed: number, solid?: boolean): void;
  spawnCoins(lane: number, z: number, count: number, spacing: number, arc: boolean): void;
  spawnFuel(lane: number, z: number): void;
  spawnStop(stop: Stop, z: number): void;
}

interface Candidate {
  kind: HazardKind;
  lane: number;
  dz: number; // offset from row position (negative = further ahead)
  speed: number;
  solid?: boolean;
}

interface Reservation {
  lane: number;
  from: number;
  to: number;
}

type Pattern = 'breather' | 'single' | 'vehicle' | 'double' | 'jumpWall' | 'convoy' | 'roadwork' | 'parked' | 'weaver';

/**
 * Procedural level generation. Works in "world distance" (metres since the run
 * started). Every candidate row is validated by `isPassable` so the game never
 * produces an unavoidable wall of traffic.
 */
export class SpawnDirector {
  cursor = 60; // first content appears after a short warm-up
  private nextStopAt: number;
  private nextFuelAt: number;
  private reservations: Reservation[] = [];
  /** Stops planned ahead of the cursor (so boarding passengers have destinations). */
  private pendingStops: Stop[] = [];
  rejected = 0;

  constructor(private rng: Random, private sink: SpawnSink, private route: PassengerRoute) {
    this.nextStopAt = 140;
    this.nextFuelAt = this.rng.range(SPAWN.fuelEvery[0], SPAWN.fuelEvery[1]);
    this.planStops();
  }

  update(distance: number, speed: number): void {
    this.planStops();
    // spawn stops whose zone is now within view
    for (const s of this.pendingStops.slice()) {
      if (s.at - SPAWN.zoneLength / 2 - 10 < distance + SPAWN.ahead) {
        this.sink.spawnStop(s, -(s.at - distance));
        this.pendingStops.splice(this.pendingStops.indexOf(s), 1);
      }
    }
    let guard = 0;
    while (this.cursor < distance + SPAWN.ahead && guard++ < 8) {
      this.generateRow(distance, speed);
    }
    this.reservations = this.reservations.filter((r) => r.to > distance - 20);
  }

  private planStops(): void {
    while (this.route.upcoming().length < 5) {
      const side: -1 | 1 = this.rng.chance(0.5) ? -1 : 1;
      const lane = side < 0 ? 0 : LANE_COUNT - 1;
      const waiting = this.rng.int(1, 4);
      const stop = this.route.plan(this.nextStopAt, side, lane, waiting);
      this.pendingStops.push(stop);
      this.reservations.push({ lane, from: stop.at - SPAWN.zoneLength / 2 - 22, to: stop.at + SPAWN.zoneLength / 2 + 6 });
      this.nextStopAt += this.rng.range(SPAWN.stopEvery[0], SPAWN.stopEvery[1]);
    }
  }

  private reserved(lane: number, at: number, halfL: number): boolean {
    for (const r of this.reservations) if (r.lane === lane && at + halfL > r.from && at - halfL < r.to) return true;
    return false;
  }

  private generateRow(distance: number, speed: number): void {
    const d = difficultyAt(this.cursor);
    const rowAt = this.cursor;
    const z = -(rowAt - distance);

    const gap = Math.max(
      speed * SPAWN.minReactionTime + PLAYER.halfLength * 2,
      (SPAWN.maxRowGap + (SPAWN.minRowGap - SPAWN.maxRowGap) * d) * this.rng.range(0.85, 1.2),
    );
    this.cursor += gap;

    let placed: Candidate[] = [];
    for (let attempt = 0; attempt < 4 && placed.length === 0; attempt++) {
      const pattern = attempt === 3 ? 'single' : this.pickPattern(d, rowAt);
      const cands = this.buildPattern(pattern, d, speed);
      if (cands.length === 0) break; // breather row
      // Only full-width "jump walls" rely on jumping. Every other jumpable is
      // planned as if it blocked its lane, so there is always a lane path that
      // needs no jump – jumping is a shortcut and a coin bonus, never forced
      // mid lane-change.
      if (pattern !== 'jumpWall') for (const c of cands) if (HAZARDS[c.kind].jumpable) c.solid = true;
      if (this.validate(cands, z, distance, speed)) placed = cands;
      else this.rejected++;
    }
    for (const c of placed) this.sink.spawnHazard(c.kind, c.lane, z + c.dz, c.speed, c.solid);

    this.placeCollectibles(rowAt, z, distance, speed, gap, placed);
  }

  private pickPattern(d: number, at: number): Pattern {
    const early = at < 350;
    return this.rng.weighted<Pattern>([
      { item: 'breather', w: 0.9 - d * 0.6 },
      { item: 'single', w: 2.2 },
      { item: 'vehicle', w: 2.6 },
      { item: 'double', w: early ? 0.2 : 0.6 + 2.2 * d },
      { item: 'jumpWall', w: early ? 0 : 0.35 + 0.8 * d },
      { item: 'convoy', w: early ? 0.3 : 0.9 + 0.5 * d },
      { item: 'roadwork', w: 0.7 },
      { item: 'parked', w: 0.8 },
      { item: 'weaver', w: early ? 0 : 0.4 + 0.9 * d },
    ]);
  }

  private trafficSpeed(kind: VehicleKind, playerSpeed: number): number {
    const [a, b] = HAZARDS[kind].speed;
    // Traffic is always noticeably slower than the jeepney so it can be overtaken.
    return Math.max(2, Math.min(this.rng.range(a, b), playerSpeed - 7));
  }

  private randomVehicle(d: number): VehicleKind {
    return this.rng.weighted<VehicleKind>([
      { item: 'car', w: 3 },
      { item: 'taxi', w: 1.2 },
      { item: 'jeepney', w: 2 },
      { item: 'tricycle', w: 1.4 },
      { item: 'motorcycle', w: 1.0 },
      { item: 'bus', w: 0.6 + d },
      { item: 'truck', w: 0.6 + d * 0.8 },
    ]);
  }

  private randomStatic(): StaticKind {
    return this.rng.weighted<StaticKind>([
      { item: 'cone', w: 2 },
      { item: 'barrier', w: 1.6 },
      { item: 'pothole', w: 1.5 },
      { item: 'roadblock', w: 1 },
    ]);
  }

  private buildPattern(p: Pattern, d: number, speed: number): Candidate[] {
    const lane = this.rng.int(0, LANE_COUNT - 1);
    const other = (l: number) => (l + this.rng.int(1, LANE_COUNT - 1)) % LANE_COUNT;
    const veh = (k: VehicleKind, l: number, dz = 0): Candidate => ({ kind: k, lane: l, dz, speed: this.trafficSpeed(k, speed) });
    const stat = (k: HazardKind, l: number, dz = 0): Candidate => ({ kind: k, lane: l, dz, speed: 0 });
    switch (p) {
      case 'breather':
        return [];
      case 'single':
        return [stat(this.randomStatic(), lane)];
      case 'vehicle':
        return [veh(this.randomVehicle(d), lane)];
      case 'double': {
        const l2 = other(lane);
        return [veh(this.randomVehicle(d), lane), this.rng.chance(0.5) ? veh(this.randomVehicle(d), l2, -this.rng.range(0, 8)) : stat(this.randomStatic(), l2)];
      }
      case 'jumpWall': {
        const k: StaticKind = this.rng.pick(['cone', 'barrier', 'pothole'] as const);
        return [0, 1, 2].map((l) => stat(k, l));
      }
      case 'convoy':
        return [veh(this.rng.chance(0.55) ? 'bus' : 'truck', lane)];
      case 'roadwork':
        // a cone line leading to a road block: too long to jump, so it blocks the lane
        return [stat('roadblock', lane, -6), ...[0, 3.2, 6.4].map((dz) => ({ ...stat('cone', lane, dz), solid: true }))];
      case 'parked': {
        const edge = this.rng.chance(0.5) ? 0 : LANE_COUNT - 1;
        return [stat(this.rng.chance(0.5) ? 'parkedCar' : 'parkedJeepney', edge)];
      }
      case 'weaver':
        return [veh('motorcycle', lane), veh('motorcycle', other(lane), -this.rng.range(6, 14))];
    }
  }

  /** Reject candidates that touch a stop reservation or make the road impassable. */
  private validate(cands: Candidate[], rowZ: number, distance: number, speed: number): boolean {
    for (const c of cands) {
      const def = HAZARDS[c.kind];
      const z = rowZ + c.dz;
      // where along the road the player will meet this object
      const closing = Math.max(0.5, speed - c.speed);
      const t = -z / closing;
      const meetAt = distance + speed * t;
      if (this.reserved(c.lane, meetAt, def.halfL + PLAYER.halfLength)) return false;
    }
    const existing = this.sink.blockers();
    const extra: PredictBlocker[] = cands.map((c) => ({
      lane: c.lane,
      z: rowZ + c.dz,
      halfL: HAZARDS[c.kind].halfL,
      speed: c.speed,
      jumpable: HAZARDS[c.kind].jumpable && !c.solid,
    }));
    // The horizon must cover every known blocker, not just the new ones: a new
    // row can push the player into a lane that an older row later closes off.
    let horizon = 1;
    for (const b of [...existing, ...extra]) {
      if (b.jumpable) continue;
      const closing = Math.max(0.5, speed - b.speed);
      horizon = Math.max(horizon, (b.halfL + PLAYER.halfLength - b.z) / closing + 0.6);
    }
    horizon = Math.min(horizon, 14);
    return isPassable([...existing, ...extra], speed, horizon, 0.05, this.sink.playerLanes());
  }

  private placeCollectibles(rowAt: number, rowZ: number, distance: number, speed: number, gap: number, placed: Candidate[]): void {
    const all = this.sink.blockers();
    const horizon = Math.min(14, (-rowZ + gap + 20) / Math.max(1, speed));
    const intervals = blockedIntervals(all, speed, horizon);

    // Fuel can
    if (rowAt >= this.nextFuelAt) {
      const lane = this.freeLaneFor(intervals, rowZ - gap * 0.5, speed, 1.5);
      if (lane >= 0) {
        this.sink.spawnFuel(lane, rowZ - gap * 0.5);
        this.nextFuelAt = rowAt + this.rng.range(SPAWN.fuelEvery[0], SPAWN.fuelEvery[1]);
      }
    }

    // Arc of coins over a jumpable obstacle
    const jumpable = placed.find((c) => HAZARDS[c.kind].jumpable && c.dz === 0 && c.kind !== 'cone');
    if (jumpable && this.rng.chance(0.7)) {
      this.sink.spawnCoins(jumpable.lane, rowZ + 6, 5, 3, true);
      return;
    }
    if (!this.rng.chance(0.75)) return;
    const count = this.rng.int(4, 8);
    const spacing = 3.2;
    const startZ = rowZ - gap * 0.25;
    const lane = this.freeLaneFor(intervals, startZ, speed, (count * spacing) / Math.max(1, speed) + 0.3, startZ);
    if (lane >= 0 && !this.reservedRange(lane, distance - startZ, count * spacing)) this.sink.spawnCoins(lane, startZ, count, spacing, false);
  }

  private reservedRange(lane: number, at: number, len: number): boolean {
    return this.reserved(lane, at + len / 2, len / 2 + 2);
  }

  private freeLaneFor(intervals: ReturnType<typeof blockedIntervals>, z: number, speed: number, duration: number, _start?: number): number {
    const t0 = -z / Math.max(1, speed) - 0.3;
    const order = [0, 1, 2].sort(() => this.rng.next() - 0.5);
    for (const l of order) if (laneClear(intervals, l, t0, t0 + duration + 0.6)) return l;
    return -1;
  }
}

export { LANES };
