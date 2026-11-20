import * as THREE from 'three';
import { ModelKit } from './ModelKit';
import { materials } from './Materials';
import { curve } from '../render/Curve';
import { SHIRT_COLORS } from './VehicleFactory';

const SKINS = ['#8d5524', '#a86b3c', '#c68642', '#7a4a26', '#b5835a'];
const HAIR = ['#111', '#2b1a10', '#1a1a1a', '#3b2416'];
const PANTS = ['#283044', '#1d3557', '#3d3d3d', '#6b4f3a', '#14213d'];

/** Low-poly commuter. Origin at the feet, facing +z (toward the camera). */
export function personKit(i: number): ModelKit {
  const k = new ModelKit();
  const shirt = SHIRT_COLORS[i % SHIRT_COLORS.length];
  const skin = SKINS[i % SKINS.length];
  const pants = PANTS[(i * 3) % PANTS.length];
  k.box('vc', 0.15, 0.75, 0.16, -0.11, 0.38, 0, pants);
  k.box('vc', 0.15, 0.75, 0.16, 0.11, 0.38, 0, pants);
  k.box('vc', 0.44, 0.58, 0.26, 0, 1.03, 0, shirt);
  k.box('vc', 0.11, 0.5, 0.12, -0.29, 1.02, 0, shirt);
  k.box('vc', 0.11, 0.5, 0.12, 0.29, 1.02, 0, skin);
  k.sphere('vc', 0.16, 0, 1.47, 0, skin, 1);
  k.sphere('vc', 0.165, 0, 1.53, -0.03, HAIR[i % HAIR.length], 1, [1, 0.7, 1]);
  if (i % 3 === 0) k.box('vc', 0.36, 0.3, 0.14, 0, 1.08, -0.2, '#5c4033'); // backpack
  if (i % 4 === 1) k.cyl('vc', 0.24, 0.24, 0.05, 10, 0, 1.68, 0, '#f1c40f'); // cap / salakot-ish hat
  return k;
}

export function buildPeople(n = 8): THREE.Group[] {
  return Array.from({ length: n }, (_, i) => personKit(i).build(materials.kit));
}

export const coinMaterial = (): THREE.MeshStandardMaterial =>
  curve(new THREE.MeshStandardMaterial({ color: '#ffc928', metalness: 1, roughness: 0.22, envMap: materials.envMap, envMapIntensity: 1.6, emissive: '#5a3a00', emissiveIntensity: 0.4 }));

export function coinGeometry(): THREE.BufferGeometry {
  const k = new ModelKit();
  k.cyl('c', 0.42, 0.42, 0.09, 20, 0, 0, 0, '#ffffff', [Math.PI / 2, 0, 0]);
  k.cyl('c', 0.3, 0.3, 0.12, 20, 0, 0, 0, '#ffe08a', [Math.PI / 2, 0, 0]);
  return k.geometry('c')!;
}

export function fuelCan(): THREE.Group {
  const k = new ModelKit();
  k.box('paint', 0.62, 0.78, 0.32, 0, 0.42, 0, '#d62828');
  k.box('paint', 0.5, 0.06, 0.34, 0, 0.66, 0, '#a4161a');
  k.box('vc', 0.36, 0.1, 0.12, -0.08, 0.88, 0, '#222');
  k.cyl('vc', 0.06, 0.07, 0.16, 8, 0.2, 0.9, 0, '#ffd60a', [0, 0, -0.4]);
  k.box('glow', 0.3, 0.3, 0.02, 0, 0.42, 0.17, '#ffd60a');
  return k.build(materials.kit);
}

/** Waiting shed with bench and roof. Sign is added separately (per-stop text). */
export function waitingShed(): THREE.Group {
  const k = new ModelKit();
  k.box('vc', 2.6, 0.12, 5.2, 0, 2.6, 0, '#0b6e4f');
  k.box('vc', 2.7, 0.06, 5.3, 0, 2.68, 0, '#ffd100');
  for (const z of [-2.3, 2.3]) for (const x of [-1.1, 1.1]) k.box('vc', 0.1, 2.6, 0.1, x, 1.3, z, '#9aa0a6');
  k.box('vc', 0.5, 0.08, 4.2, 0.7, 0.5, 0, '#8d6e63');
  k.box('vc', 0.1, 0.5, 4.2, 0.95, 0.75, 0, '#8d6e63');
  k.box('vc', 2.6, 0.1, 5.2, 0, 0.05, 0, '#9e9e9e');
  return k.build(materials.kit);
}
