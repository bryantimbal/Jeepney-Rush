import * as THREE from 'three';
import { curve } from '../render/Curve';
import { ModelKit } from './ModelKit';
import { materials } from './Materials';
import { jeepneyHeaderTexture, jeepneyRearTexture, jeepneySideTexture, type JeepneySkin } from './JeepneySkins';
import { FONT_DISPLAY, fitText, newCanvas, canvasTexture } from './Textures';

const CHROME = '#e9edf2';
const DARK = '#1c1d22';
const GLASS = '#1d2a3a';

/** Adds a small chrome horse ornament (the jeepney's signature hood mascot). */
function horse(k: ModelKit, x: number, y: number, z: number, s = 1): void {
  const c = CHROME;
  k.box('chrome', 0.1 * s, 0.11 * s, 0.26 * s, x, y + 0.14 * s, z, c);
  k.box('chrome', 0.07 * s, 0.2 * s, 0.08 * s, x, y + 0.27 * s, z - 0.12 * s, c, [0.5, 0, 0]);
  k.box('chrome', 0.07 * s, 0.08 * s, 0.15 * s, x, y + 0.36 * s, z - 0.2 * s, c, [0.2, 0, 0]);
  k.box('chrome', 0.03 * s, 0.07 * s, 0.03 * s, x, y + 0.41 * s, z - 0.15 * s, c); // ear
  // rearing front legs, planted hind legs
  for (const lx of [-0.035, 0.035]) {
    k.box('chrome', 0.03 * s, 0.15 * s, 0.03 * s, x + lx * s, y + 0.2 * s, z - 0.17 * s, c, [-0.9, 0, 0]);
    k.box('chrome', 0.03 * s, 0.16 * s, 0.03 * s, x + lx * s, y + 0.06 * s, z + 0.1 * s, c);
  }
  k.box('chrome', 0.03 * s, 0.14 * s, 0.03 * s, x, y + 0.13 * s, z + 0.16 * s, c, [0.6, 0, 0]); // tail
  k.box('chrome', 0.16 * s, 0.03 * s, 0.34 * s, x, y + 0.01 * s, z, c); // plinth
}

export interface JeepneyModel {
  root: THREE.Group;
  /** Sub-group that tilts/bounces without moving the shadow blob. */
  body: THREE.Group;
  wheels: THREE.Object3D[];
  brakeLights: THREE.Mesh;
  dispose(): void;
}

/**
 * The hero vehicle: long hood with horse mascots, chrome grille and bumper,
 * multiple ornamental headlights, painted stainless body, open rear doorway,
 * roof signboard and streamer antennas.
 */
export function buildJeepney(skin: JeepneySkin, opts: { shadows?: boolean } = {}): JeepneyModel {
  const root = new THREE.Group();
  const body = new THREE.Group();
  root.add(body);
  const k = new ModelKit();
  const textures: THREE.Texture[] = [];
  const ownMats: THREE.Material[] = [];

  // --- chassis & running gear
  k.box('vc', 1.7, 0.26, 5.9, 0, 0.56, -0.15, DARK);
  k.box('vc', 0.22, 0.22, 0.6, 0.62, 0.5, 3.0, '#333');
  // --- cab (lower body in front of passenger compartment)
  k.box('paint', 2.0, 1.0, 1.05, 0, 1.15, -1.53, skin.body);
  k.box('glass', 1.86, 0.5, 1.0, 0, 1.9, -1.5, GLASS);
  // windshield + chrome frame
  k.box('glass', 1.8, 0.52, 0.05, 0, 1.92, -2.06, '#2a3d55', [0.12, 0, 0]);
  k.box('chrome', 1.96, 0.06, 0.08, 0, 2.18, -2.07, CHROME);
  k.box('chrome', 1.96, 0.06, 0.08, 0, 1.66, -2.07, CHROME);
  k.box('chrome', 0.06, 0.5, 0.08, 0, 1.92, -2.08, CHROME);
  // window pillars along the passenger section
  for (let i = 0; i <= 5; i++) k.box('paint', 2.03, 0.5, 0.09, 0, 1.9, -1.0 + i * 0.65, skin.trim);
  // rear corner posts (leave the header board visible)
  for (const sx of [-1, 1]) k.box('paint', 0.12, 0.5, 0.09, sx * 0.955, 1.9, 2.87, skin.trim);
  k.box('glass', 1.94, 0.5, 3.88, 0, 1.9, 0.95, GLASS);
  // roof with overhang, rails and front light bar
  k.box('paint', 2.14, 0.14, 5.05, 0, 2.22, 0.42, skin.roof);
  k.box('chrome', 0.05, 0.08, 4.9, 1.04, 2.33, 0.45, CHROME);
  k.box('chrome', 0.05, 0.08, 4.9, -1.04, 2.33, 0.45, CHROME);
  for (let i = 0; i < 7; i++) k.sphere('glow', 0.055, -0.75 + i * 0.25, 2.33, -2.02, ['#ff3b3b', '#ffd23f', '#3bceac', '#4d9de0', '#ff8c42', '#ffffff', '#e15fed'][i], 0);
  k.box('chrome', 1.78, 0.38, 0.05, 0, 2.47, -1.92, CHROME); // signboard frame
  // roof horns
  for (const x of [-0.45, 0.45]) {
    k.cyl('chrome', 0.03, 0.09, 0.5, 8, x, 2.38, -1.75, CHROME, [Math.PI / 2, 0, 0]);
  }
  // --- hood & front fenders
  k.box('paint', 1.56, 0.55, 1.2, 0, 1.08, -2.62, skin.hood);
  k.box('paint', 1.5, 0.06, 1.16, 0, 1.38, -2.62, skin.hood);
  k.box('chrome', 0.06, 0.04, 1.2, 0, 1.42, -2.62, CHROME);
  for (const sx of [-1, 1]) {
    k.box('paint', 0.42, 0.12, 1.15, sx * 0.94, 1.02, -2.2, skin.hood);
    k.box('paint', 0.42, 0.4, 0.12, sx * 0.94, 0.86, -1.67, skin.hood);
    k.box('vc', 0.32, 0.3, 0.06, sx * 0.94, 0.6, 2.62, '#111'); // mudflap
    k.box('glow', 0.12, 0.06, 0.02, sx * 0.94, 0.62, 2.66, '#ff2a2a');
  }
  // --- grille, bumper, bull bar
  k.box('chrome', 1.34, 0.6, 0.08, 0, 1.0, -3.24, CHROME);
  for (let i = 0; i < 9; i++) k.box('vc', 0.05, 0.48, 0.02, -0.52 + i * 0.13, 1.0, -3.29, '#202126');
  k.box('chrome', 2.15, 0.2, 0.24, 0, 0.56, -3.36, CHROME);
  k.cyl('chrome', 0.04, 0.04, 1.6, 8, 0, 0.8, -3.42, CHROME, [0, 0, Math.PI / 2]);
  for (const x of [-0.7, 0.7]) k.cyl('chrome', 0.04, 0.04, 0.5, 8, x, 0.72, -3.42, CHROME);
  // --- ornamental headlights: 2 big + 4 small, all chrome-ringed
  for (const sx of [-1, 1]) {
    k.cyl('chrome', 0.19, 0.19, 0.08, 16, sx * 0.62, 1.12, -3.2, CHROME, [Math.PI / 2, 0, 0]);
    k.cyl('glow', 0.15, 0.15, 0.06, 16, sx * 0.62, 1.12, -3.25, '#fff7d6', [Math.PI / 2, 0, 0]);
    for (let j = 0; j < 2; j++) {
      k.cyl('chrome', 0.085, 0.085, 0.06, 12, sx * (0.78 + j * 0.2), 1.12, -2.1 - j * 0.2, CHROME, [Math.PI / 2, 0, 0]);
      k.cyl('glow', 0.065, 0.065, 0.05, 12, sx * (0.78 + j * 0.2), 1.12, -2.14 - j * 0.2, j ? '#ffcf3f' : '#ffffff', [Math.PI / 2, 0, 0]);
    }
    k.sphere('glow', 0.05, sx * 0.42, 0.7, -3.5, '#ffb000', 0);
    // side mirror on a stalk
    k.cyl('chrome', 0.015, 0.015, 0.4, 6, sx * 1.12, 1.75, -2.0, CHROME, [0, 0, sx * 0.9]);
    k.box('chrome', 0.06, 0.22, 0.14, sx * 1.27, 1.88, -2.0, CHROME);
    // streamer antennas on the bumper corners
    k.cyl('chrome', 0.012, 0.012, 1.3, 5, sx * 1.0, 1.25, -3.38, CHROME);
    for (let t = 0; t < 4; t++) k.box('vc', 0.025, 0.18, 0.03, sx * 1.0 + (t - 1.5) * 0.03, 1.85, -3.38, skin.stripes[t % skin.stripes.length], [0, 0, (t - 1.5) * 0.3]);
  }
  // signature horse mascots
  horse(k, -0.36, 1.41, -3.0, 1.15);
  horse(k, 0.36, 1.41, -3.0, 1.15);
  horse(k, 0, 1.41, -2.6, 0.85);
  // --- rear: doorway, grab bars, step, tail lights, bumper
  k.box('vc', 0.86, 1.08, 0.06, 0, 1.26, 2.9, '#140f1a');
  k.box('glow', 0.5, 0.05, 0.02, 0, 1.78, 2.92, '#ffe9a8');
  // interior benches visible through the doorway
  for (const sx of [-1, 1]) {
    k.box('vc', 0.4, 0.12, 3.4, sx * 0.72, 1.05, 1.0, '#8b1e2d');
    k.cyl('chrome', 0.025, 0.025, 1.35, 6, sx * 0.47, 1.4, 2.95, CHROME);
    k.box('glow', 0.09, 0.42, 0.04, sx * 0.94, 1.25, 2.93, '#ff1f1f');
    k.box('glow', 0.09, 0.14, 0.04, sx * 0.94, 0.95, 2.93, '#ffb000');
  }
  k.box('chrome', 1.05, 0.06, 0.38, 0, 0.62, 3.06, CHROME);
  k.box('chrome', 2.05, 0.14, 0.12, 0, 0.62, 3.0, CHROME);

  const kitMats = materials.kit;
  const merged = k.build(kitMats, { castShadow: !!opts.shadows });
  body.add(merged);

  // --- textured panels (painted art)
  const sideTex = jeepneySideTexture(skin);
  const rearTex = jeepneyRearTexture(skin);
  const headTex = jeepneyHeaderTexture(skin.rearText, skin);
  textures.push(sideTex, rearTex, headTex);
  const plain = curve(new THREE.MeshStandardMaterial({ color: skin.body, metalness: 0.3, roughness: 0.35, envMap: materials.envMap, envMapIntensity: 0.7 }));
  const sideMat = curve(new THREE.MeshStandardMaterial({ map: sideTex, metalness: 0.3, roughness: 0.35, envMap: materials.envMap, envMapIntensity: 0.7 }));
  const rearMat = curve(new THREE.MeshStandardMaterial({ map: rearTex, metalness: 0.3, roughness: 0.35, envMap: materials.envMap, envMapIntensity: 0.7 }));
  const headMat = curve(new THREE.MeshBasicMaterial({ map: headTex }));
  ownMats.push(plain, sideMat, rearMat, headMat);
  // passenger compartment: px, nx, py, ny, pz(rear), nz(front)
  const comp = new THREE.Mesh(new THREE.BoxGeometry(2.0, 1.0, 3.92), [sideMat, sideMat, plain, plain, rearMat, plain]);
  comp.position.set(0, 1.15, 0.95);
  comp.castShadow = !!opts.shadows;
  body.add(comp);
  const header = new THREE.Mesh(new THREE.PlaneGeometry(2.0, 0.42), headMat);
  header.position.set(0, 1.92, 2.96);
  body.add(header);

  // front route signboard
  const signC = newCanvas(512, 96);
  const sctx = signC.getContext('2d')!;
  sctx.fillStyle = '#fffbe6';
  sctx.fillRect(0, 0, 512, 96);
  sctx.fillStyle = skin.stripes[0];
  sctx.fillRect(0, 0, 512, 10);
  sctx.fillRect(0, 86, 512, 10);
  sctx.fillStyle = '#14213d';
  sctx.textAlign = 'center';
  sctx.textBaseline = 'middle';
  fitText(sctx, skin.route, 256, 50, 480, 54, FONT_DISPLAY);
  const signTex = canvasTexture(signC);
  textures.push(signTex);
  const signMat = curve(new THREE.MeshBasicMaterial({ map: signTex }));
  ownMats.push(signMat);
  const sign = new THREE.Mesh(new THREE.PlaneGeometry(1.7, 0.32), signMat);
  sign.position.set(0, 2.47, -1.95);
  sign.rotation.y = Math.PI;
  body.add(sign);

  // brake light overlay (brightens while braking)
  const brakeMat = curve(new THREE.MeshBasicMaterial({ color: '#ff2020', transparent: true, opacity: 0, toneMapped: false }));
  ownMats.push(brakeMat);
  const brakeLights = new THREE.Mesh(new THREE.BoxGeometry(2.0, 0.2, 0.02), brakeMat);
  brakeLights.position.set(0, 2.25, 2.98);
  body.add(brakeLights);

  // --- wheels (separate so they can spin)
  const wheels: THREE.Object3D[] = [];
  const wk = new ModelKit();
  wk.wheel('vc', 0.43, 0.32, 0, 0, 0, '#17171a', 14);
  wk.wheel('chrome', 0.24, 0.34, 0, 0, 0, CHROME, 10);
  wk.box('vc', 0.36, 0.08, 0.08, 0, 0, 0, '#3a3a40');
  const tireGeo = wk.geometry('vc')!;
  const hubGeo = wk.geometry('chrome')!;
  for (const [x, z] of [[-0.93, -2.18], [0.93, -2.18], [-0.93, 1.72], [0.93, 1.72]]) {
    const w = new THREE.Group();
    const t = new THREE.Mesh(tireGeo, kitMats.vc);
    t.castShadow = !!opts.shadows;
    w.add(t, new THREE.Mesh(hubGeo, kitMats.chrome));
    w.position.set(x, 0.43, z);
    root.add(w);
    wheels.push(w);
  }

  // contact shadow blob (always on, cheap)
  const blob = new THREE.Mesh(new THREE.PlaneGeometry(2.8, 7.2), materials.blob);
  blob.rotation.x = -Math.PI / 2;
  blob.position.y = 0.02;
  blob.name = 'blob';
  root.add(blob);

  return {
    root,
    body,
    wheels,
    brakeLights,
    dispose() {
      textures.forEach((t) => t.dispose());
      ownMats.forEach((m) => m.dispose());
      root.traverse((o) => {
        if (o instanceof THREE.Mesh && o.geometry !== tireGeo && o.geometry !== hubGeo) o.geometry.dispose();
      });
      tireGeo.dispose();
      hubGeo.dispose();
    },
  };
}
