import * as THREE from 'three';
import { ModelKit } from './ModelKit';
import { materials } from './Materials';
import type { HazardKind } from '../game/Kinds';

const CHROME = '#e9edf2';
const TIRE = '#16161a';
const GLASS = '#1b2735';

const CAR_COLORS = ['#c1121f', '#f8f9fa', '#adb5bd', '#212529', '#1d4ed8', '#2a9d8f', '#e9c46a', '#6a4c93'];
const JEEP_COLORS: [string, string, string][] = [
  ['#e63946', '#ffd166', '#1d3557'],
  ['#06d6a0', '#ef476f', '#073b4c'],
  ['#ffb703', '#219ebc', '#023047'],
  ['#9b5de5', '#f15bb5', '#fee440'],
  ['#8ecae6', '#fb8500', '#023047'],
  ['#d7dbe2', '#2a9d8f', '#e76f51'],
];
const BUS_COLORS: [string, string][] = [
  ['#f8f9fa', '#e63946'],
  ['#1d3557', '#ffd166'],
  ['#2a9d8f', '#f8f9fa'],
  ['#ffb703', '#023047'],
];
const SHIRTS = ['#e63946', '#457b9d', '#2a9d8f', '#f4a261', '#ffd166', '#9b5de5', '#f8f9fa', '#06d6a0'];

function wheels(k: ModelKit, xs: number, zs: number[], r: number, w = 0.28): void {
  for (const z of zs)
    for (const s of [-1, 1]) {
      k.wheel('vc', r, w, s * xs, r, z, TIRE, 10);
      k.wheel('chrome', r * 0.5, w + 0.02, s * xs, r, z, CHROME, 8);
    }
}

function tail(k: ModelKit, halfW: number, y: number, z: number, h = 0.16): void {
  for (const s of [-1, 1]) {
    k.box('glow', 0.32, h, 0.04, s * (halfW - 0.2), y, z, '#ff2121');
    k.box('glow', 0.1, h, 0.04, s * (halfW - 0.42), y, z, '#ff9f1c');
  }
}

function heads(k: ModelKit, halfW: number, y: number, z: number): void {
  for (const s of [-1, 1]) k.box('glow', 0.3, 0.14, 0.04, s * (halfW - 0.25), y, z, '#fff8dc');
}

function car(color: string, taxi = false): ModelKit {
  const k = new ModelKit();
  k.box('paint', 1.84, 0.62, 4.36, 0, 0.62, 0, color);
  k.box('paint', 1.7, 0.12, 1.2, 0, 0.98, -1.4, color);
  k.box('glass', 1.64, 0.56, 2.2, 0, 1.2, 0.25, GLASS);
  k.box('paint', 1.66, 0.08, 1.9, 0, 1.5, 0.3, color);
  k.box('vc', 1.9, 0.18, 0.2, 0, 0.42, 2.18, '#2b2d33');
  k.box('vc', 1.9, 0.18, 0.2, 0, 0.42, -2.18, '#2b2d33');
  tail(k, 0.92, 0.78, 2.19);
  heads(k, 0.92, 0.72, -2.19);
  if (taxi) {
    k.box('glow', 0.7, 0.2, 0.3, 0, 1.64, 0.3, '#ffd60a');
    k.box('vc', 1.86, 0.1, 4.0, 0, 0.75, 0, '#111');
  }
  wheels(k, 0.86, [-1.35, 1.35], 0.34);
  return k;
}

function trafficJeepney(c: [string, string, string]): ModelKit {
  const [body, stripe, roof] = c;
  const k = new ModelKit();
  k.box('vc', 1.6, 0.25, 5.6, 0, 0.55, 0, '#1d1d22');
  k.box('paint', 2.0, 1.0, 3.9, 0, 1.15, 0.9, body);
  k.box('paint', 2.02, 0.12, 3.92, 0, 0.9, 0.9, stripe);
  k.box('paint', 2.02, 0.07, 3.92, 0, 1.25, 0.9, roof);
  k.box('paint', 2.0, 1.0, 1.0, 0, 1.15, -1.55, body);
  k.box('glass', 1.92, 0.5, 4.9, 0, 1.9, 0.4, GLASS);
  for (let z = -1.9; z <= 2.8; z += 0.7) k.box('paint', 2.0, 0.5, 0.08, 0, 1.9, z, stripe);
  k.box('paint', 2.12, 0.14, 5.0, 0, 2.22, 0.4, roof);
  k.box('paint', 1.5, 0.52, 1.1, 0, 1.08, -2.6, stripe);
  k.box('chrome', 1.3, 0.55, 0.08, 0, 1.0, -3.18, CHROME);
  k.box('chrome', 2.1, 0.18, 0.2, 0, 0.56, -3.3, CHROME);
  k.box('vc', 0.86, 1.4, 0.05, 0, 1.43, 2.86, '#120d18');
  k.box('chrome', 2.04, 0.14, 0.12, 0, 0.62, 2.95, CHROME);
  k.box('vc', 1.7, 0.3, 0.05, 0, 2.0, 2.88, '#1b1033');
  k.box('glow', 1.2, 0.08, 0.02, 0, 2.0, 2.91, stripe);
  for (const s of [-1, 1]) {
    k.box('glow', 0.09, 0.4, 0.04, s * 0.94, 1.25, 2.88, '#ff1f1f');
    k.cyl('chrome', 0.15, 0.15, 0.06, 12, s * 0.6, 1.1, -3.2, CHROME, [Math.PI / 2, 0, 0]);
    k.cyl('glow', 0.11, 0.11, 0.05, 12, s * 0.6, 1.1, -3.24, '#fff7d6', [Math.PI / 2, 0, 0]);
  }
  k.box('chrome', 0.1, 0.25, 0.25, 0, 1.5, -2.95, CHROME);
  wheels(k, 0.93, [-2.15, 1.7], 0.42, 0.3);
  return k;
}

function bus(c: [string, string]): ModelKit {
  const [body, stripe] = c;
  const k = new ModelKit();
  k.box('paint', 2.36, 2.5, 10.8, 0, 1.75, 0, body);
  k.box('glass', 2.4, 0.95, 9.4, 0, 2.15, 0.3, GLASS);
  k.box('paint', 2.4, 0.3, 10.82, 0, 1.15, 0, stripe);
  k.box('paint', 2.3, 0.12, 10.6, 0, 3.06, 0, '#e9ecef');
  k.box('vc', 1.6, 0.4, 0.4, 0, 3.25, 1.5, '#ced4da'); // roof aircon
  k.box('glass', 2.2, 1.3, 0.05, 0, 2.1, -5.41, GLASS);
  k.box('vc', 1.7, 0.36, 0.05, 0, 2.84, 5.41, '#111');
  k.box('glow', 1.5, 0.2, 0.02, 0, 2.84, 5.44, '#ffa62b'); // route LED
  k.box('glass', 1.9, 0.8, 0.05, 0, 2.15, 5.41, GLASS);
  tail(k, 1.18, 1.0, 5.42, 0.4);
  heads(k, 1.18, 0.9, -5.42);
  k.box('vc', 2.4, 0.28, 0.22, 0, 0.55, 5.4, '#2b2d33');
  wheels(k, 1.05, [-3.6, 3.3], 0.52, 0.34);
  return k;
}

function truck(color: string): ModelKit {
  const k = new ModelKit();
  k.box('paint', 2.24, 1.9, 1.9, 0, 1.55, -2.65, color);
  k.box('glass', 2.1, 0.75, 0.06, 0, 1.95, -3.62, GLASS);
  k.box('vc', 2.0, 0.4, 7.2, 0, 0.75, 0, '#26262b');
  k.box('vc', 2.3, 2.6, 5.0, 0, 2.05, 1.0, '#f1f1ee');
  k.box('paint', 2.32, 0.4, 5.02, 0, 1.2, 1.0, color);
  k.box('vc', 2.2, 2.4, 0.05, 0, 2.05, 3.52, '#d8d8d3');
  k.box('vc', 0.05, 2.3, 0.06, 0, 2.05, 3.55, '#8a8a85');
  for (let i = 0; i < 6; i++) k.box('glow', 0.16, 0.12, 0.03, -0.9 + i * 0.36, 0.72, 3.55, i % 2 ? '#ff2121' : '#ffd23f');
  tail(k, 1.12, 0.75, 3.56);
  heads(k, 1.12, 1.0, -3.62);
  wheels(k, 1.0, [-2.6, 1.4, 2.6], 0.5, 0.34);
  return k;
}

function rider(k: ModelKit, z: number, shirt: string, helmet: string): void {
  k.box('vc', 0.4, 0.55, 0.28, 0, 1.25, z, shirt);
  k.sphere('paint', 0.17, 0, 1.68, z - 0.05, helmet, 1);
  k.box('vc', 0.12, 0.5, 0.12, -0.15, 0.85, z - 0.2, '#283044', [1.2, 0, 0]);
  k.box('vc', 0.12, 0.5, 0.12, 0.15, 0.85, z - 0.2, '#283044', [1.2, 0, 0]);
  k.box('vc', 0.1, 0.42, 0.1, -0.22, 1.25, z - 0.3, shirt, [1.1, 0, 0]);
  k.box('vc', 0.1, 0.42, 0.1, 0.22, 1.25, z - 0.3, shirt, [1.1, 0, 0]);
}

function motorcycle(color: string, shirt: string): ModelKit {
  const k = new ModelKit();
  k.wheel('vc', 0.32, 0.12, 0, 0.32, -0.7, TIRE);
  k.wheel('vc', 0.32, 0.14, 0, 0.32, 0.7, TIRE);
  k.box('paint', 0.34, 0.36, 1.1, 0, 0.66, 0, color);
  k.box('vc', 0.3, 0.12, 0.6, 0, 0.9, 0.25, '#111');
  k.box('chrome', 0.7, 0.04, 0.04, 0, 1.12, -0.55, CHROME);
  k.box('glow', 0.14, 0.08, 0.04, 0, 0.8, 0.98, '#ff2121');
  k.box('glow', 0.16, 0.12, 0.04, 0, 0.95, -0.85, '#fff8dc');
  rider(k, 0.15, shirt, color);
  return k;
}

function tricycle(color: string, shirt: string): ModelKit {
  const k = motorcycle(color, shirt);
  // sidecar on the right with the iconic canopy roof
  k.box('paint', 0.95, 0.85, 1.7, 0.85, 0.85, 0.05, color);
  k.box('glass', 0.9, 0.42, 1.2, 0.85, 1.48, 0.15, GLASS);
  k.box('paint', 1.95, 0.08, 2.1, 0.45, 1.9, 0.05, color);
  k.box('vc', 0.06, 0.95, 0.06, -0.45, 1.45, -0.9, '#222');
  k.box('vc', 0.06, 0.95, 0.06, -0.45, 1.45, 0.95, '#222');
  k.box('paint', 0.97, 0.12, 1.72, 0.85, 1.25, 0.05, '#ffd166');
  k.wheel('vc', 0.3, 0.14, 1.2, 0.3, 0.2, TIRE);
  k.box('glow', 0.18, 0.1, 0.04, 1.15, 0.72, 0.92, '#ff2121');
  return k;
}

function cone(): ModelKit {
  const k = new ModelKit();
  k.box('vc', 0.6, 0.06, 0.6, 0, 0.03, 0, '#e85d04');
  k.cyl('vc', 0.05, 0.25, 0.72, 10, 0, 0.42, 0, '#ff6d00');
  k.cyl('vc', 0.15, 0.19, 0.12, 10, 0, 0.42, 0, '#ffffff');
  return k;
}

function barrier(): ModelKit {
  const k = new ModelKit();
  for (const x of [-0.95, 0.95]) {
    k.box('vc', 0.08, 0.95, 0.08, x, 0.47, -0.18, '#666', [0.25, 0, 0]);
    k.box('vc', 0.08, 0.95, 0.08, x, 0.47, 0.18, '#666', [-0.25, 0, 0]);
  }
  for (let i = 0; i < 6; i++) k.box('vc', 0.38, 0.3, 0.06, -0.95 + i * 0.38, 0.78, 0, i % 2 ? '#ffffff' : '#ff6d00');
  for (const x of [-0.95, 0.95]) k.sphere('glow', 0.07, x, 1.0, 0, '#ffb703', 0);
  return k;
}

function pothole(): ModelKit {
  const k = new ModelKit();
  k.cyl('vc', 0.95, 1.0, 0.04, 12, 0, 0.025, 0, '#5b5a57', undefined);
  k.cyl('vc', 0.78, 0.8, 0.05, 12, 0, 0.03, 0, '#151417');
  k.cyl('vc', 0.3, 0.35, 0.06, 7, 0.35, 0.035, 0.2, '#2a6f97'); // puddle
  return k;
}

function roadblock(): ModelKit {
  const k = new ModelKit();
  for (const x of [-1.15, 1.15]) {
    k.box('vc', 0.12, 2.1, 0.12, x, 1.05, 0, '#555');
    k.box('vc', 0.5, 0.08, 0.6, x, 0.04, 0, '#333');
  }
  for (let i = 0; i < 8; i++) k.box('vc', 0.3, 0.42, 0.08, -1.05 + i * 0.3, 1.5, 0, i % 2 ? '#ffffff' : '#d00000');
  k.box('vc', 2.4, 0.5, 0.08, 0, 0.75, 0, '#ffd60a');
  k.box('vc', 1.6, 0.18, 0.09, 0, 0.75, -0.01, '#111');
  for (const x of [-1.15, 1.15]) k.sphere('glow', 0.11, x, 2.2, 0, '#ff3b30', 0);
  return k;
}

/** Pre-built geometry variants per hazard kind. Instances share geometry. */
export class VehicleLibrary {
  private templates = new Map<HazardKind, THREE.Group[]>();

  build(shadows: boolean): void {
    const mk = (k: ModelKit) => k.build(materials.kit, { castShadow: shadows });
    const put = (kind: HazardKind, groups: THREE.Group[]) => this.templates.set(kind, groups);
    put('car', CAR_COLORS.map((c) => mk(car(c))));
    put('taxi', [mk(car('#f8f9fa', true)), mk(car('#ffd60a', true))]);
    put('jeepney', JEEP_COLORS.map((c) => mk(trafficJeepney(c))));
    put('bus', BUS_COLORS.map((c) => mk(bus(c))));
    put('truck', ['#e63946', '#1d4ed8', '#2a9d8f', '#f4a261'].map((c) => mk(truck(c))));
    put('motorcycle', CAR_COLORS.slice(0, 5).map((c, i) => mk(motorcycle(c, SHIRTS[i]))));
    put('tricycle', ['#e63946', '#1d4ed8', '#06d6a0', '#ffb703', '#9b5de5'].map((c, i) => mk(tricycle(c, SHIRTS[i + 2]))));
    put('cone', [mk(cone())]);
    put('barrier', [mk(barrier())]);
    put('pothole', [pothole().build(materials.kit)]);
    put('roadblock', [mk(roadblock())]);
    put('parkedCar', CAR_COLORS.map((c) => mk(car(c))));
    put('parkedJeepney', JEEP_COLORS.map((c) => mk(trafficJeepney(c))));
  }

  /** Returns a new instance sharing geometry with the template. */
  instance(kind: HazardKind, variant: number): THREE.Group {
    const list = this.templates.get(kind)!;
    const t = list[variant % list.length];
    const g = t.clone();
    // contact shadow
    const def = { w: 2.4, l: 5 };
    const box = new THREE.Box3().setFromObject(t);
    def.w = Math.max(0.6, box.max.x - box.min.x) * 1.35;
    def.l = Math.max(0.6, box.max.z - box.min.z) * 1.2;
    if (kind !== 'pothole') {
      const blob = new THREE.Mesh(blobGeo, materials.blob);
      blob.scale.set(def.w, def.l, 1);
      blob.rotation.x = -Math.PI / 2;
      blob.position.y = 0.025;
      g.add(blob);
    }
    return g;
  }

  variants(kind: HazardKind): number {
    return this.templates.get(kind)?.length ?? 1;
  }
}

const blobGeo = new THREE.PlaneGeometry(1, 1);

export const SHIRT_COLORS = SHIRTS;
