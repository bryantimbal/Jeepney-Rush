import * as THREE from 'three';
import { LANES, SPAWN, PLAYER } from '../config';
import { Pool } from '../core/Pool';
import type { Random } from '../core/Random';
import { materials } from '../art/Materials';
import { ModelKit } from '../art/ModelKit';
import { buildPeople, waitingShed } from '../art/PropFactory';
import { canvasTexture, drawStopSign, newCanvas } from '../art/Textures';
import { SIDEWALK_INNER, SIDEWALK_Y } from '../art/SceneryFactory';
import { curve } from '../render/Curve';
import type { PassengerRoute, ServeResult, Stop } from './logic/PassengerRoute';

interface Person {
  obj: THREE.Group;
  variant: number;
  state: 'wait' | 'board' | 'alight';
  x: number;
  y: number;
  z: number;
  t: number;
  fx: number;
  fz: number;
  tx: number;
  wave: number;
}

interface StopView {
  stop: Stop;
  shed: ShedView;
  z: number;
  waiting: Person[];
  done: boolean;
}

interface ShedView {
  root: THREE.Group;
  zone: THREE.Mesh;
  signCanvas: HTMLCanvasElement;
  signTex: THREE.CanvasTexture;
  arrowPick: THREE.Mesh;
  arrowDrop: THREE.Mesh;
}

/**
 * Visual + gameplay layer for jeepney stops: waiting sheds, "PARA" zones,
 * commuters waving you down, boarding and alighting animations.
 */
export class PassengerManager {
  private views: StopView[] = [];
  private shedPool: Pool<ShedView>;
  private personPools: Pool<THREE.Group>[] = [];
  private flying: Person[] = [];
  private time = 0;

  constructor(private root: THREE.Object3D, public route: PassengerRoute, public rng: Random) {
    const people = buildPeople(8);
    this.personPools = people.map((tpl) => new Pool(() => {
      const g = tpl.clone();
      g.traverse((o) => (o.frustumCulled = false));
      return g;
    }, (o) => root.add(o), (o) => root.remove(o)));
    const arrowGeo = (color: string) => {
      const k = new ModelKit();
      k.cyl('glow', 0.55, 0.0, 0.8, 4, 0, 0, 0, color, [Math.PI, Math.PI / 4, 0]);
      k.box('glow', 0.36, 0.7, 0.36, 0, 0.7, 0, color);
      return k.geometry('glow')!;
    };
    const pickGeo = arrowGeo('#ffd100');
    const dropGeo = arrowGeo('#2bff88');
    this.shedPool = new Pool<ShedView>(() => {
      const root = new THREE.Group();
      const shed = waitingShed();
      root.add(shed);
      const signCanvas = newCanvas(256, 128);
      const signTex = canvasTexture(signCanvas);
      const sign = new THREE.Mesh(new THREE.PlaneGeometry(2.0, 1.0), curve(new THREE.MeshBasicMaterial({ map: signTex })));
      sign.name = 'sign';
      sign.position.set(0, 3.5, 2.7);
      root.add(sign);
      const zone = new THREE.Mesh(new THREE.PlaneGeometry(2.5, SPAWN.zoneLength), materials.zone);
      zone.rotation.x = -Math.PI / 2;
      zone.position.y = 0.03;
      const arrowPick = new THREE.Mesh(pickGeo, materials.glow);
      const arrowDrop = new THREE.Mesh(dropGeo, materials.glow);
      for (const o of [root, zone, arrowPick, arrowDrop]) o.traverse((c) => (c.frustumCulled = false));
      return { root, zone, signCanvas, signTex, arrowPick, arrowDrop };
    }, (v) => this.root.add(v.root, v.zone, v.arrowPick, v.arrowDrop), (v) => this.root.remove(v.root, v.zone, v.arrowPick, v.arrowDrop));
  }

  spawnStop(stop: Stop, z: number): void {
    const shed = this.shedPool.acquire();
    drawStopSign(shed.signCanvas, stop.name);
    shed.signTex.needsUpdate = true;
    const sx = stop.side * (SIDEWALK_INNER + 1.7);
    shed.root.position.set(sx, SIDEWALK_Y, z);
    shed.root.rotation.y = stop.side < 0 ? 0 : Math.PI;
    const sign = shed.root.getObjectByName('sign')!;
    sign.rotation.y = stop.side < 0 ? 0 : Math.PI; // keep sign facing the camera
    sign.position.z = stop.side < 0 ? 2.7 : -2.7;
    shed.zone.position.set(LANES[stop.lane], 0.03, z);
    const waiting: Person[] = [];
    for (let i = 0; i < stop.waiting; i++) {
      const variant = this.rng.int(0, this.personPools.length - 1);
      const obj = this.personPools[variant].acquire();
      const p: Person = { obj, variant, state: 'wait', x: stop.side * (SIDEWALK_INNER + 0.5 + this.rng.range(0, 0.6)), y: 0, z: z + (i - stop.waiting / 2) * 1.0, t: 0, fx: 0, fz: 0, tx: 0, wave: this.rng.range(0, 6) };
      obj.rotation.y = stop.side < 0 ? 0.7 : -0.7;
      waiting.push(p);
    }
    this.views.push({ stop, shed, z, waiting, done: false });
  }

  clear(): void {
    for (const v of this.views) {
      for (const p of v.waiting) this.personPools[p.variant].release(p.obj);
      this.shedPool.release(v.shed);
    }
    for (const p of this.flying) this.personPools[p.variant].release(p.obj);
    this.views.length = 0;
    this.flying.length = 0;
  }

  update(dt: number, speed: number, px: number, onServe: (r: ServeResult, s: Stop) => void, onMiss: (lost: number, s: Stop) => void): void {
    this.time += dt;
    const half = SPAWN.zoneLength / 2;
    for (let i = this.views.length - 1; i >= 0; i--) {
      const v = this.views[i];
      v.z += speed * dt;
      const sh = v.shed;
      sh.root.position.z = v.z;
      sh.root.position.y = SIDEWALK_Y;
      sh.zone.position.z = v.z;
      const dropping = this.route.alightingAt(v.stop.id) > 0;
      const showArrow = !v.done;
      const arrow = dropping ? sh.arrowDrop : sh.arrowPick;
      sh.arrowPick.visible = showArrow && !dropping;
      sh.arrowDrop.visible = showArrow && dropping;
      arrow.position.set(LANES[v.stop.lane], 3.2 + Math.sin(this.time * 5) * 0.3, v.z - half + 2);
      arrow.rotation.y = this.time * 2;

      // trigger: jeepney inside the zone, in the right lane
      if (!v.done && Math.abs(v.z) < half + PLAYER.halfLength - 1 && Math.abs(px - LANES[v.stop.lane]) < 1.1) {
        v.done = true;
        const res = this.route.serve(v.stop);
        onServe(res, v.stop);
        // board animation for the first `boarded` commuters
        for (let b = 0; b < res.boarded && v.waiting.length; b++) {
          const p = v.waiting.shift()!;
          p.state = 'board';
          p.t = 0;
          p.fx = p.x;
          p.fz = p.z;
          this.flying.push(p);
        }
        for (let a = 0; a < res.alighted; a++) {
          const variant = this.rng.int(0, this.personPools.length - 1);
          const obj = this.personPools[variant].acquire();
          const p: Person = { obj, variant, state: 'alight', x: px, y: 1, z: PLAYER.halfLength + 0.4 + a * 0.4, t: 0, fx: px, fz: 0, tx: v.stop.side * (SIDEWALK_INNER + 0.6 + a * 0.4), wave: a };
          this.flying.push(p);
        }
      } else if (!v.done && v.z - half > PLAYER.halfLength) {
        v.done = true;
        onMiss(this.route.miss(v.stop), v.stop);
      }

      for (const p of v.waiting) {
        p.z += speed * dt;
        p.wave += dt;
        // commuters wave the jeepney down ("Para!")
        p.obj.position.set(p.x, SIDEWALK_Y + Math.max(0, Math.sin(p.wave * 6)) * (v.done ? 0 : 0.12), p.z);
        p.obj.rotation.z = v.done ? 0 : Math.sin(p.wave * 8) * 0.06;
      }

      if (v.z > 40) {
        for (const p of v.waiting) this.personPools[p.variant].release(p.obj);
        this.shedPool.release(sh);
        this.views.splice(i, 1);
      }
    }

    for (let i = this.flying.length - 1; i >= 0; i--) {
      const p = this.flying[i];
      p.t += dt;
      if (p.state === 'board') {
        const k = Math.min(1, p.t / 0.45);
        // hop into the jeepney's rear doorway (which stays at z ~ +3)
        p.x = p.fx + (px - p.fx) * k;
        p.z = p.fz + (PLAYER.halfLength + 0.3 - p.fz) * k;
        p.y = Math.sin(k * Math.PI) * 2.2 + k * 0.6;
        p.obj.position.set(p.x, p.y, p.z);
        p.obj.rotation.y = Math.PI;
        p.obj.scale.setScalar(1 - k * 0.5);
        if (k >= 1) {
          p.obj.scale.setScalar(1);
          this.personPools[p.variant].release(p.obj);
          this.flying.splice(i, 1);
        }
      } else {
        const k = Math.min(1, p.t / 0.5);
        p.z += speed * dt;
        p.x = p.fx + (p.tx - p.fx) * k;
        p.y = Math.max(SIDEWALK_Y, 0.9 + Math.sin(k * Math.PI) * 1.4 - k * 0.7);
        p.obj.position.set(p.x, p.y, p.z);
        p.obj.rotation.y = 0;
        if (p.z > 25) {
          this.personPools[p.variant].release(p.obj);
          this.flying.splice(i, 1);
        }
      }
    }
  }
}
