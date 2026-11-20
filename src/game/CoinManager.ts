import * as THREE from 'three';
import { LANES, SPAWN } from '../config';
import { Pool } from '../core/Pool';
import { coinGeometry, coinMaterial, fuelCan } from '../art/PropFactory';
import { collects } from './logic/Collision';

interface Coin {
  x: number;
  y: number;
  z: number;
  baseY: number;
  magnet: boolean;
}

interface Fuel {
  obj: THREE.Group;
  x: number;
  z: number;
  t: number;
}

/**
 * Coins (instanced: a whole road of coins is one draw call) and fuel cans.
 */
export class CoinManager {
  private coins: Coin[] = [];
  private mesh: THREE.InstancedMesh;
  private readonly MAX = 160;
  private spin = 0;
  private m4 = new THREE.Matrix4();
  private q = new THREE.Quaternion();
  private v = new THREE.Vector3();
  private one = new THREE.Vector3(1, 1, 1);
  private fuels: Fuel[] = [];
  private fuelPool: Pool<THREE.Group>;
  /** Radius within which coins fly toward the jeepney (used by the Phase 2 magnet). */
  magnetRadius = 0;

  constructor(root: THREE.Object3D) {
    this.mesh = new THREE.InstancedMesh(coinGeometry(), coinMaterial(), this.MAX);
    this.mesh.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
    this.mesh.count = 0;
    this.mesh.frustumCulled = false;
    this.mesh.castShadow = false;
    root.add(this.mesh);
    this.fuelPool = new Pool(() => {
      const g = fuelCan();
      g.traverse((o) => (o.frustumCulled = false));
      return g;
    }, (o) => root.add(o), (o) => root.remove(o));
  }

  spawnCoins(lane: number, z: number, count: number, spacing: number, arc: boolean): void {
    for (let i = 0; i < count && this.coins.length < this.MAX; i++) {
      const t = count > 1 ? i / (count - 1) : 0;
      const y = arc ? 1.0 + Math.sin(t * Math.PI) * 2.4 : 1.0;
      this.coins.push({ x: LANES[lane], y, baseY: y, z: z - i * spacing, magnet: false });
    }
  }

  spawnFuel(lane: number, z: number): void {
    const obj = this.fuelPool.acquire();
    this.fuels.push({ obj, x: LANES[lane], z, t: 0 });
  }

  clear(): void {
    this.coins.length = 0;
    this.mesh.count = 0;
    for (const f of this.fuels) this.fuelPool.release(f.obj);
    this.fuels.length = 0;
  }

  /** Moves pickups and returns how many coins / fuel cans were collected this frame. */
  update(dt: number, speed: number, px: number, py: number, onCoin: (c: { x: number; y: number; z: number }) => void, onFuel: () => void): void {
    this.spin += dt * 4;
    let n = 0;
    for (let i = this.coins.length - 1; i >= 0; i--) {
      const c = this.coins[i];
      c.z += speed * dt;
      if (this.magnetRadius > 0 && !c.magnet && Math.abs(c.z) < this.magnetRadius && c.z < 2) c.magnet = true;
      if (c.magnet) {
        const k = Math.min(1, dt * 12);
        c.x += (px - c.x) * k;
        c.y += (py + 1.2 - c.y) * k;
        c.z += (0 - c.z) * k;
      }
      if (collects(px, py, c.x, c.y, c.z, 1.0)) {
        onCoin(c);
        this.coins[i] = this.coins[this.coins.length - 1];
        this.coins.pop();
        continue;
      }
      if (c.z > SPAWN.despawnBehind) {
        this.coins[i] = this.coins[this.coins.length - 1];
        this.coins.pop();
        continue;
      }
      this.v.set(c.x, c.y + Math.sin(this.spin * 0.8 + c.z * 0.3) * 0.08, c.z);
      this.q.setFromAxisAngle(THREE.Object3D.DEFAULT_UP, this.spin + c.z * 0.15);
      this.m4.compose(this.v, this.q, this.one);
      this.mesh.setMatrixAt(n++, this.m4);
    }
    this.mesh.count = n;
    this.mesh.instanceMatrix.needsUpdate = true;

    for (let i = this.fuels.length - 1; i >= 0; i--) {
      const f = this.fuels[i];
      f.t += dt;
      f.z += speed * dt;
      f.obj.position.set(f.x, 0.6 + Math.sin(f.t * 3) * 0.15, f.z);
      f.obj.rotation.y = f.t * 1.5;
      if (collects(px, py, f.x, 1.0, f.z, 1.2)) {
        onFuel();
        this.fuelPool.release(f.obj);
        this.fuels.splice(i, 1);
      } else if (f.z > SPAWN.despawnBehind) {
        this.fuelPool.release(f.obj);
        this.fuels.splice(i, 1);
      }
    }
  }

  get activeCoins(): number {
    return this.coins.length;
  }
}
