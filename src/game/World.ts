import * as THREE from 'three';
import { LANES, PLAYER, STOP_NAMES } from '../config';
import { Random } from '../core/Random';
import type { VehicleLibrary } from '../art/VehicleFactory';
import { RoadManager } from './RoadManager';
import { ObstacleManager, TrafficManager, type Hazard } from './Hazards';
import { CoinManager } from './CoinManager';
import { PassengerManager } from './PassengerManager';
import { Effects } from './Effects';
import { PassengerRoute } from './logic/PassengerRoute';
import { SpawnDirector, type SpawnSink } from './logic/SpawnDirector';
import { isVehicle, type HazardKind } from './Kinds';
import type { PredictBlocker } from './logic/Occupancy';

/**
 * Container for everything that lives in the 3D world during a run.
 * Re-created logic state per run; meshes are pooled and reused.
 */
export class World implements SpawnSink {
  readonly root = new THREE.Group();
  readonly road: RoadManager;
  readonly traffic: TrafficManager;
  readonly obstacles: ObstacleManager;
  readonly coins: CoinManager;
  readonly effects: Effects;
  passengers!: PassengerManager;
  route!: PassengerRoute;
  director!: SpawnDirector;
  rng: Random;
  private blockerBuf: PredictBlocker[] = [];

  constructor(scene: THREE.Scene, lib: VehicleLibrary, seed: number) {
    scene.add(this.root);
    this.rng = new Random(seed);
    this.road = new RoadManager(this.root, this.rng);
    this.traffic = new TrafficManager(this.root, lib, this.rng);
    this.obstacles = new ObstacleManager(this.root, lib, this.rng);
    this.coins = new CoinManager(this.root);
    this.effects = new Effects(scene);
    this.traffic.allBlockers = () => this.blockers();
    this.traffic.playerLanes = () => this.currentLanes;
    this.route = new PassengerRoute(PLAYER.capacity, this.rng, STOP_NAMES);
    this.passengers = new PassengerManager(this.root, this.route, this.rng);
  }

  /** Reset for a new run. */
  reset(seed: number, capacity: number): void {
    this.rng = new Random(seed);
    this.traffic.clear();
    this.obstacles.clear();
    this.coins.clear();
    this.passengers.clear();
    this.effects.clear();
    this.route = new PassengerRoute(capacity, this.rng, STOP_NAMES);
    // managers keep their mesh pools; only logic state and RNG are swapped
    this.passengers.route = this.route;
    this.passengers.rng = this.traffic.rng = this.obstacles.rng = this.rng;
    this.director = new SpawnDirector(this.rng, this, this.route);
    this.road.layout();
  }

  // ---- SpawnSink
  /** Set by the game each frame so the generator plans from the player's real lane. */
  currentLanes: number[] = [1];
  playerLanes(): number[] {
    return this.currentLanes;
  }
  blockers(): PredictBlocker[] {
    this.blockerBuf.length = 0;
    this.traffic.blockers(this.blockerBuf);
    this.obstacles.blockers(this.blockerBuf);
    return this.blockerBuf.slice();
  }
  spawnHazard(kind: HazardKind, lane: number, z: number, speed: number, solid?: boolean): void {
    (isVehicle(kind) ? this.traffic : this.obstacles).spawn(kind, lane, z, speed, solid);
  }
  spawnCoins(lane: number, z: number, count: number, spacing: number, arc: boolean): void {
    this.coins.spawnCoins(lane, z, count, spacing, arc);
  }
  spawnFuel(lane: number, z: number): void {
    this.coins.spawnFuel(lane, z);
  }
  spawnStop(stop: Parameters<SpawnSink['spawnStop']>[0], z: number): void {
    this.passengers.spawnStop(stop, z);
  }

  allHazards(): Hazard[] {
    return [...this.traffic.items, ...this.obstacles.items];
  }

  /** Ambient update used on the menu screen (no spawning, slow cruise). */
  idle(dt: number, speed: number): void {
    this.road.update(dt, speed);
    this.effects.update(dt, speed, 0);
  }
}

export { LANES };
