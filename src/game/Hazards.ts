import * as THREE from 'three';
import { LANES, SPAWN } from '../config';
import { Pool } from '../core/Pool';
import type { Random } from '../core/Random';
import type { VehicleLibrary } from '../art/VehicleFactory';
import { HAZARDS, isVehicle, type HazardDef, type HazardKind } from './Kinds';
import type { PredictBlocker } from './logic/Occupancy';
import { isPassable } from './logic/Occupancy';

export interface Hazard {
  kind: HazardKind;
  def: HazardDef;
  obj: THREE.Group;
  poolKey: string;
  lane: number;
  x: number;
  z: number;
  speed: number;
  hit: boolean;
  /** Jumpable, but treated as lane-blocking by the level generator. */
  solid: boolean;
  /** Lane change state for weaving traffic. */
  lc: { from: number; to: number; t: number; signal: number } | null;
  /** Physics after being knocked by the jeepney. */
  knock: { vx: number; vy: number; vz: number; spin: number; y: number } | null;
  t: number;
}

/**
 * Shared pooled-hazard behaviour. TrafficManager and ObstacleManager extend it.
 */
abstract class HazardManagerBase {
  readonly items: Hazard[] = [];
  private pools = new Map<string, Pool<THREE.Group>>();

  constructor(protected root: THREE.Object3D, protected lib: VehicleLibrary, public rng: Random) {}

  protected acquire(kind: HazardKind): { obj: THREE.Group; key: string } {
    const variant = this.rng.int(0, this.lib.variants(kind) - 1);
    const key = `${kind}:${variant}`;
    let pool = this.pools.get(key);
    if (!pool) {
      pool = new Pool(() => {
        const g = this.lib.instance(kind, variant);
        g.traverse((o) => (o.frustumCulled = false));
        return g;
      }, (o) => {
        o.visible = true;
        this.root.add(o);
      }, (o) => {
        o.visible = false;
        this.root.remove(o);
      });
      this.pools.set(key, pool);
    }
    return { obj: pool.acquire(), key };
  }

  spawn(kind: HazardKind, lane: number, z: number, speed: number, solid = false): Hazard {
    const { obj, key } = this.acquire(kind);
    const h: Hazard = { kind, def: HAZARDS[kind], obj, poolKey: key, lane, x: LANES[lane], z, speed, hit: false, solid, lc: null, knock: null, t: this.rng.range(0, 10) };
    obj.position.set(h.x, 0, z);
    obj.rotation.set(0, 0, 0);
    this.items.push(h);
    this.onSpawn(h);
    return h;
  }

  protected onSpawn(_h: Hazard): void {}

  protected despawn(i: number): void {
    const h = this.items[i];
    this.pools.get(h.poolKey)?.release(h.obj);
    this.items.splice(i, 1);
  }

  clear(): void {
    for (let i = this.items.length - 1; i >= 0; i--) this.despawn(i);
  }

  blockers(out: PredictBlocker[]): void {
    for (const h of this.items) {
      if (h.hit) continue;
      out.push({ lane: h.lc ? h.lc.from : h.lane, lane2: h.lc ? h.lc.to : undefined, z: h.z, halfL: h.def.halfL, speed: h.speed, jumpable: h.def.jumpable && !h.solid });
    }
  }

  update(dt: number, playerSpeed: number): void {
    for (let i = this.items.length - 1; i >= 0; i--) {
      const h = this.items[i];
      h.t += dt;
      h.z += (playerSpeed - h.speed) * dt;
      if (h.knock) {
        const k = h.knock;
        k.vy -= 25 * dt;
        k.y = Math.max(0, k.y + k.vy * dt);
        if (k.y === 0) {
          k.vx *= 0.9;
          k.vz *= 0.9;
          k.vy = Math.abs(k.vy) > 2 ? -k.vy * 0.3 : 0;
        }
        h.x += k.vx * dt;
        h.z += k.vz * dt;
        h.obj.rotation.y += k.spin * dt;
        h.obj.rotation.x += k.spin * 0.5 * dt * (k.y > 0 ? 1 : 0);
        h.obj.position.y = k.y;
      }
      this.animate(h, dt);
      h.obj.position.x = h.x;
      h.obj.position.z = h.z;
      if (h.z > SPAWN.despawnBehind + h.def.halfL || h.z < -SPAWN.ahead - 120) this.despawn(i);
    }
  }

  protected animate(_h: Hazard, _dt: number): void {}

  /** React to being hit: knock it away so the player is not stuck inside it. */
  knock(h: Hazard, fromX: number): void {
    h.hit = true;
    const away = Math.sign(h.x - fromX) || (this.rng.chance(0.5) ? 1 : -1);
    if (isVehicle(h.kind) || h.kind.startsWith('parked')) {
      h.knock = { vx: away * 3, vy: 2, vz: -8, spin: away * 1.2, y: 0 };
    } else {
      h.knock = { vx: away * this.rng.range(3, 7), vy: this.rng.range(6, 10), vz: -this.rng.range(8, 16), spin: this.rng.range(-10, 10), y: 0 };
    }
  }
}

/** Moving traffic: cars, jeepneys, buses, trucks, motorcycles, tricycles. */
export class TrafficManager extends HazardManagerBase {
  private weaveTimer = 0;
  /** Supplied by the world: all predicted blockers (for safe lane changes). */
  allBlockers: () => PredictBlocker[] = () => [];
  playerLanes: () => number[] = () => [1];

  protected animate(h: Hazard, dt: number): void {
    // body bob
    if (!h.knock) h.obj.position.y = Math.abs(Math.sin(h.t * (h.kind === 'motorcycle' ? 9 : 6))) * 0.025;
    if (h.lc) {
      const lc = h.lc;
      if (lc.signal > 0) {
        lc.signal -= dt;
        // indicator blink handled by a small wobble so the player reads intent
        h.obj.rotation.z = Math.sin(h.t * 25) * 0.01;
      } else {
        lc.t = Math.min(1, lc.t + dt / 0.9);
        const e = lc.t * lc.t * (3 - 2 * lc.t);
        h.x = LANES[lc.from] + (LANES[lc.to] - LANES[lc.from]) * e;
        h.obj.rotation.y = (lc.to > lc.from ? -1 : 1) * Math.sin(lc.t * Math.PI) * 0.18;
        if (lc.t >= 1) {
          h.lane = lc.to;
          h.lc = null;
          h.obj.rotation.y = 0;
          h.obj.rotation.z = 0;
        }
      }
    }
  }

  update(dt: number, playerSpeed: number): void {
    super.update(dt, playerSpeed);
    // occasionally a motorcycle or tricycle swerves into a neighbouring lane
    this.weaveTimer -= dt;
    if (this.weaveTimer > 0) return;
    this.weaveTimer = this.rng.range(0.8, 1.8);
    const candidates = this.items.filter((h) => !h.hit && !h.lc && (h.kind === 'motorcycle' || h.kind === 'tricycle' || h.kind === 'car') && h.z < -55 && h.z > -150);
    if (!candidates.length) return;
    const h = this.rng.pick(candidates);
    const dir = h.lane === 0 ? 1 : h.lane === LANES.length - 1 ? -1 : this.rng.chance(0.5) ? -1 : 1;
    const to = h.lane + dir;
    // the target lane must be free around the vehicle, and the road must stay passable
    const near = this.items.some((o) => o !== h && (o.lane === to || o.lc?.to === to) && Math.abs(o.z - h.z) < o.def.halfL + h.def.halfL + 6);
    if (near) return;
    h.lc = { from: h.lane, to, t: 0, signal: 0.7 };
    const all = this.allBlockers();
    if (!isPassable(all, playerSpeed, 8, 0.05, this.playerLanes())) h.lc = null;
  }
}

/** Static obstacles: cones, barriers, potholes, road blocks, parked vehicles. */
export class ObstacleManager extends HazardManagerBase {
  protected onSpawn(h: Hazard): void {
    if (h.kind === 'pothole') h.obj.rotation.y = this.rng.range(0, Math.PI);
    if (h.kind === 'parkedCar' || h.kind === 'parkedJeepney') {
      // hug the curb a little
      h.x += h.lane === 0 ? -0.35 : 0.35;
    }
  }
}
