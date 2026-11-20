import * as THREE from 'three';
import { ModelKit } from './ModelKit';
import { materials } from './Materials';
import { Random } from '../core/Random';
import { SHOP_SIGNS, signUV } from './Textures';
import { personKit } from './PropFactory';

export const CHUNK_LENGTH = 40;
export const SIDEWALK_INNER = 4.75;
export const BUILDING_X = 7.8;
/** Sidewalk surface height; chunks are lifted by this much. */
export const SIDEWALK_Y = 0.2;

const WALLS = ['#f6bd60', '#f7ede2', '#84a59d', '#f28482', '#bde0fe', '#cdb4db', '#ffc8dd', '#a3c4bc', '#e9edc9', '#ffd6a5', '#caffbf', '#9bf6ff', '#fdffb6', '#ffadad'];
const AWNINGS = ['#e63946', '#2a9d8f', '#f4a261', '#1d3557', '#ffb703', '#8338ec', '#06d6a0'];

/** Box with UVs scaled so the facade texture tiles every `tile` metres. */
function facadeBox(w: number, h: number, d: number, tile = 6): THREE.BoxGeometry {
  const g = new THREE.BoxGeometry(w, h, d);
  const uv = g.attributes.uv as THREE.BufferAttribute;
  const dims: [number, number][] = [[d, h], [d, h], [w, d], [w, d], [w, h], [w, h]];
  for (let f = 0; f < 6; f++)
    for (let v = 0; v < 4; v++) {
      const i = f * 4 + v;
      uv.setXY(i, uv.getX(i) * (dims[f][0] / tile), uv.getY(i) * (dims[f][1] / tile));
    }
  return g;
}

function signPlane(index: number, w: number, h: number): THREE.PlaneGeometry {
  const g = new THREE.PlaneGeometry(w, h);
  const [u0, v0, u1, v1] = signUV(index);
  const uv = g.attributes.uv as THREE.BufferAttribute;
  // PlaneGeometry uv order: (0,1) (1,1) (0,0) (1,0)
  uv.setXY(0, u0, v1);
  uv.setXY(1, u1, v1);
  uv.setXY(2, u0, v0);
  uv.setXY(3, u1, v0);
  return g;
}

/**
 * Builds one 40 m slice of Manila street scenery (both sides). A handful of
 * variants are pre-built and recycled, so runtime cost is zero allocations.
 */
export function buildManilaChunk(seed: number, opts: { crossing: boolean }): THREE.Group {
  const rng = new Random(seed);
  const k = new ModelKit();
  const L = CHUNK_LENGTH;

  for (const side of [-1, 1]) {
    const sx = side;
    // ----- buildings, packed along the street
    let z = -L / 2;
    while (z < L / 2 - 2) {
      const w = Math.min(rng.range(6, 11), L / 2 - z);
      const h = rng.pick([6, 9, 9, 12, 15, 18, 24]) + rng.range(-0.5, 0.5);
      const d = rng.range(7, 11);
      const cz = z + w / 2;
      const cx = sx * (BUILDING_X + d / 2 + rng.range(0, 0.6));
      const wall = rng.pick(WALLS);
      k.add('facade', facadeBox(d, h, w - 0.3), wall, [cx, h / 2, cz]);
      // roof parapet + water tank
      k.box('vc', d + 0.2, 0.4, w - 0.1, cx, h + 0.2, cz, new THREE.Color(wall).multiplyScalar(0.8));
      if (rng.chance(0.5)) {
        k.cyl('vc', 0.7, 0.7, 1.4, 8, cx + rng.range(-1, 1), h + 1.1, cz + rng.range(-1.5, 1.5), rng.pick(['#1d4ed8', '#334155', '#e5e7eb']));
      }
      // ground-floor storefront
      const front = sx * BUILDING_X;
      k.box('vc', 0.2, 2.9, w - 0.5, front + sx * 0.05, 1.45, cz, '#2f2a2a');
      k.box('vc', 0.15, 2.4, w - 1.4, front - sx * 0.05, 1.3, cz, rng.pick(['#3a2f2a', '#4a3f35', '#26323d']));
      // shelves with colourful goods (sari-sari vibes)
      for (let s = 0; s < 3; s++) {
        k.box('vc', 0.12, 0.25, w - 1.8, front - sx * 0.12, 0.6 + s * 0.6, cz, rng.pick(['#ffd166', '#ef476f', '#06d6a0', '#118ab2', '#f78c6b']));
      }
      // awning (striped)
      const aw = rng.pick(AWNINGS);
      for (let s = 0; s < Math.floor(w - 0.6); s++) {
        k.box('vc', 1.6, 0.08, 1, front - sx * 0.8, 3.15, z + 0.8 + s, s % 2 ? aw : '#ffffff', [0, 0, sx * 0.28]);
      }
      // blade sign facing the traffic (+z) – readable from the camera
      if (rng.chance(0.75)) {
        const si = rng.int(0, SHOP_SIGNS.length - 1);
        const sw = 2.3, sh = 0.86;
        const sxp = front - sx * (sw / 2 + 0.2);
        const sy = rng.range(4.2, Math.min(h - 1, 7));
        const bz = cz + w / 2 - 0.6;
        k.add('signs', signPlane(si, sw, sh), '#fff', [sxp, sy, bz + 0.035]);
        k.add('signs', signPlane(si, sw, sh), '#fff', [sxp, sy, bz - 0.035], [0, Math.PI, 0]);
        k.box('vc', sw + 0.12, sh + 0.12, 0.05, sxp, sy, bz, '#222');
        k.box('vc', 0.06, 0.06, 1.2, front - sx * 0.6, sy + sh / 2 + 0.05, bz, '#444', [0, Math.PI / 2, 0]);
      }
      // rooftop billboard
      if (h < 16 && rng.chance(0.3)) {
        const si = rng.int(0, SHOP_SIGNS.length - 1);
        const by = h + 3.2;
        k.add('signs', signPlane(si, 7, 2.6), '#fff', [cx, by, cz + 0.13]);
        k.box('vc', 7.2, 2.8, 0.2, cx, by, cz, '#333');
        k.box('vc', 0.2, 2.2, 0.2, cx - 2.5, h + 1.0, cz, '#555');
        k.box('vc', 0.2, 2.2, 0.2, cx + 2.5, h + 1.0, cz, '#555');
      }
      z += w;
    }
    // ----- tall skyline towers far behind
    for (let i = 0; i < 2; i++) {
      if (!rng.chance(0.55)) continue;
      const th = rng.range(28, 60);
      const tw = rng.range(10, 16);
      k.add('facade', facadeBox(tw, th, tw, 7.5), rng.pick(['#d6e2e9', '#bcd4e6', '#e2ece9', '#c9d6df', '#fde4cf']), [sx * rng.range(28, 44), th / 2, rng.range(-L / 2, L / 2)]);
    }
    // ----- power pole + tangled wires (very Manila)
    const px = sx * (SIDEWALK_INNER + 0.5);
    k.cyl('vc', 0.13, 0.17, 9, 6, px, 4.5, -L / 2 + 2, '#6d6875');
    k.box('vc', 2.2, 0.14, 0.14, px - sx * 0.3, 8.4, -L / 2 + 2, '#4a4e69');
    if (rng.chance(0.5)) k.cyl('vc', 0.35, 0.35, 0.9, 8, px + sx * 0.3, 7.4, -L / 2 + 2, '#8d99ae');
    for (let wi = 0; wi < 5; wi++) {
      const wx = px - sx * (0.9 - wi * 0.35);
      const wy = 8.2 - wi * 0.25 + rng.range(-0.1, 0.1);
      k.box('vc', 0.03, 0.03, L, wx, wy, 2, '#1b1b1b', [rng.range(-0.012, 0.012), 0, 0]);
    }
    // ----- street lamp
    const lz = rng.range(-L / 2 + 8, L / 2 - 6);
    k.cyl('vc', 0.07, 0.09, 6, 6, sx * (SIDEWALK_INNER + 0.4), 3, lz, '#495057');
    k.box('vc', 1.6, 0.08, 0.1, sx * (SIDEWALK_INNER - 0.3), 6, lz, '#495057');
    k.box('glow', 0.5, 0.1, 0.3, sx * (SIDEWALK_INNER - 0.9), 5.93, lz, '#fff1c1');
    // ----- trees
    if (rng.chance(0.6)) {
      const tz = rng.range(-L / 2 + 4, L / 2 - 4);
      const tx = sx * (SIDEWALK_INNER + 1.4);
      k.cyl('vc', 0.18, 0.25, 3, 6, tx, 1.5, tz, '#6b4226');
      const leaf = rng.pick(['#2d6a4f', '#40916c', '#52b788', '#386641']);
      k.sphere('vc', 1.6, tx, 3.8, tz, leaf, 0, [1.1, 0.8, 1.1]);
      k.sphere('vc', 1.1, tx + sx * 0.6, 4.6, tz + 0.5, leaf, 0);
    }
    // ----- street vendor cart with umbrella
    if (rng.chance(0.45)) {
      const vz = rng.range(-L / 2 + 5, L / 2 - 5);
      const vx = sx * (SIDEWALK_INNER + 1.2);
      k.box('vc', 0.9, 0.8, 1.5, vx, 0.75, vz, rng.pick(['#e9c46a', '#2a9d8f', '#e76f51']));
      k.wheel('vc', 0.25, 0.1, vx + 0.5, 0.25, vz, '#222');
      k.cyl('vc', 0.03, 0.03, 1.6, 5, vx, 1.9, vz, '#333');
      const um = rng.pick(['#ef476f', '#ffd166', '#06d6a0', '#118ab2']);
      k.cyl('vc', 0.05, 1.3, 0.5, 8, vx, 2.75, vz, um);
      k.cyl('vc', 0.3, 0.3, 0.3, 8, vx, 1.3, vz - 0.3, '#c0c0c0'); // fishball wok
    }
    // ----- idle pedestrians
    const nPeds = rng.int(0, 3);
    for (let i = 0; i < nPeds; i++) {
      const pk = personKit(rng.int(0, 7));
      const pg = pk.geometry('vc');
      if (pg) k.add('vc', pg, null, [sx * rng.range(SIDEWALK_INNER + 0.8, BUILDING_X - 0.6), 0, rng.range(-L / 2, L / 2)], [0, rng.range(0, Math.PI * 2), 0]);
    }
  }

  if (opts.crossing) {
    for (let x = -3.9; x <= 3.9; x += 0.9) k.box('vc', 0.5, 0.02, 3.2, x, 0.012 - SIDEWALK_Y, 0, '#f1f1f1');
    // crossing signs
    for (const sx of [-1, 1]) {
      k.cyl('vc', 0.05, 0.05, 2.6, 6, sx * (SIDEWALK_INNER + 0.3), 1.3, 2.4, '#888');
      k.box('vc', 0.7, 0.7, 0.05, sx * (SIDEWALK_INNER + 0.3), 2.7, 2.4, '#1d4ed8', [0, 0, Math.PI / 4]);
    }
  }

  const group = k.build({ ...materials.kit, signs: materials.signs });
  group.traverse((o) => {
    if (o instanceof THREE.Mesh) {
      o.matrixAutoUpdate = true;
      o.frustumCulled = false; // curve shader displaces geometry; culling would pop
    }
  });
  return group;
}
