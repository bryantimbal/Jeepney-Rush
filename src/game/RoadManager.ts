import * as THREE from 'three';
import { ROAD_HALF_WIDTH } from '../config';
import { materials } from '../art/Materials';
import { ROAD_TEX_METRES } from '../art/Textures';
import { buildManilaChunk, CHUNK_LENGTH, BUILDING_X, SIDEWALK_Y } from '../art/SceneryFactory';
import type { Random } from '../core/Random';

const ROAD_LEN = 320;
const ROAD_NEAR = 24;

/**
 * Endless road: scrolling textures for asphalt/sidewalk/curb, plus a ring of
 * pre-built scenery chunks recycled from behind the camera to the horizon.
 */
export class RoadManager {
  private chunks: THREE.Group[] = [];
  private active: THREE.Group[] = [];
  private free: THREE.Group[] = [];
  private scroll = 0;

  constructor(private root: THREE.Object3D, private rng: Random) {
    const center = ROAD_NEAR - ROAD_LEN / 2;
    const road = new THREE.Mesh(new THREE.PlaneGeometry(ROAD_HALF_WIDTH * 2, ROAD_LEN, 1, 90), materials.road);
    road.rotation.x = -Math.PI / 2;
    road.position.z = center;
    road.receiveShadow = true;
    materials.roadTex.repeat.set(1, ROAD_LEN / ROAD_TEX_METRES);

    const swW = BUILDING_X - ROAD_HALF_WIDTH - 0.1;
    materials.sidewalkTex.repeat.set(swW / 2, ROAD_LEN / 2);
    materials.curbTex.repeat.set(ROAD_LEN / 4, 1);
    const meshes: THREE.Object3D[] = [road];
    for (const s of [-1, 1]) {
      const sw = new THREE.Mesh(new THREE.PlaneGeometry(swW, ROAD_LEN, 1, 90), materials.sidewalk);
      sw.rotation.x = -Math.PI / 2;
      sw.position.set(s * (ROAD_HALF_WIDTH + 0.05 + swW / 2), SIDEWALK_Y, center);
      sw.receiveShadow = true;
      // vertical curb face toward the road; u runs toward -z on both sides
      const cg = new THREE.PlaneGeometry(ROAD_LEN, 0.24);
      if (s > 0) {
        const uv = cg.attributes.uv as THREE.BufferAttribute;
        for (let i = 0; i < uv.count; i++) uv.setX(i, 1 - uv.getX(i));
      }
      const curb = new THREE.Mesh(cg, materials.curb);
      curb.rotation.y = s < 0 ? Math.PI / 2 : -Math.PI / 2;
      curb.position.set(s * (ROAD_HALF_WIDTH + 0.05), 0.1, center);
      meshes.push(sw, curb);
    }
    const ground = new THREE.Mesh(new THREE.PlaneGeometry(220, ROAD_LEN, 1, 60), materials.vc);
    const cols = new Float32Array(ground.geometry.attributes.position.count * 3).fill(0.42);
    ground.geometry.setAttribute('color', new THREE.BufferAttribute(cols, 3));
    ground.rotation.x = -Math.PI / 2;
    ground.position.set(0, -0.05, center);
    meshes.push(ground);
    for (const m of meshes) {
      m.frustumCulled = false;
      this.root.add(m);
    }

    // pre-build scenery chunk variants
    for (let i = 0; i < 9; i++) {
      const c = buildManilaChunk(1000 + i * 7919, { crossing: i === 2 || i === 6 });
      c.visible = false;
      c.position.y = SIDEWALK_Y;
      this.chunks.push(c);
      this.free.push(c);
      this.root.add(c);
    }
    this.layout();
  }

  /** Place chunks from just behind the camera to the horizon. */
  layout(): void {
    for (const c of this.active) {
      c.visible = false;
      this.free.push(c);
    }
    this.active.length = 0;
    let z = ROAD_NEAR - CHUNK_LENGTH / 2;
    while (z > -ROAD_LEN + ROAD_NEAR) {
      this.placeChunk(z);
      z -= CHUNK_LENGTH;
    }
  }

  private placeChunk(z: number): void {
    const idx = this.rng.int(0, this.free.length - 1);
    const c = this.free.splice(idx, 1)[0];
    c.position.z = z;
    c.visible = true;
    this.active.push(c);
  }

  update(dt: number, speed: number): void {
    const dz = speed * dt;
    this.scroll += dz;
    const t = materials.roadTex;
    t.offset.y = (t.offset.y + dz / ROAD_TEX_METRES) % 1;
    materials.sidewalkTex.offset.y = (materials.sidewalkTex.offset.y + dz / 2) % 1;
    materials.curbTex.offset.x = (materials.curbTex.offset.x + dz / 4) % 1;
    let farthest = Infinity;
    for (const c of this.active) {
      c.position.z += dz;
      farthest = Math.min(farthest, c.position.z);
    }
    for (let i = this.active.length - 1; i >= 0; i--) {
      const c = this.active[i];
      if (c.position.z - CHUNK_LENGTH / 2 > ROAD_NEAR) {
        this.active.splice(i, 1);
        c.visible = false;
        this.free.push(c);
        this.placeChunk(farthest - CHUNK_LENGTH);
        farthest -= CHUNK_LENGTH;
      }
    }
  }
}
