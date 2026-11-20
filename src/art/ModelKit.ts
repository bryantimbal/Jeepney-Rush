import * as THREE from 'three';
import { mergeGeometries } from 'three/examples/jsm/utils/BufferGeometryUtils.js';

/**
 * Builds low-poly models from primitives and merges them per material so each
 * vehicle costs only 1–4 draw calls. Colours are baked into vertex colours.
 */
export class ModelKit {
  private parts = new Map<string, THREE.BufferGeometry[]>();
  private tmpColor = new THREE.Color();

  add(key: string, geom: THREE.BufferGeometry, color: THREE.ColorRepresentation | null, pos: [number, number, number], rot?: [number, number, number], scale?: [number, number, number]): this {
    let g = geom.index ? geom.toNonIndexed() : geom;
    if (g === geom) g = geom.clone();
    const m = new THREE.Matrix4();
    const q = new THREE.Quaternion().setFromEuler(new THREE.Euler(...(rot ?? [0, 0, 0])));
    m.compose(new THREE.Vector3(...pos), q, new THREE.Vector3(...(scale ?? [1, 1, 1])));
    g.applyMatrix4(m);
    const n = g.attributes.position.count;
    if (color !== null || !g.attributes.color) {
      this.tmpColor.set(color ?? '#ffffff');
      const colors = new Float32Array(n * 3);
      for (let i = 0; i < n; i++) {
        colors[i * 3] = this.tmpColor.r;
        colors[i * 3 + 1] = this.tmpColor.g;
        colors[i * 3 + 2] = this.tmpColor.b;
      }
      g.setAttribute('color', new THREE.BufferAttribute(colors, 3));
    }
    if (!g.attributes.uv) g.setAttribute('uv', new THREE.BufferAttribute(new Float32Array(n * 2), 2));
    for (const k of Object.keys(g.attributes)) if (!['position', 'normal', 'uv', 'color'].includes(k)) g.deleteAttribute(k);
    (this.parts.get(key) ?? this.parts.set(key, []).get(key)!).push(g);
    return this;
  }

  box(key: string, w: number, h: number, d: number, x: number, y: number, z: number, color: THREE.ColorRepresentation, rot?: [number, number, number]): this {
    return this.add(key, new THREE.BoxGeometry(w, h, d), color, [x, y, z], rot);
  }

  cyl(key: string, rt: number, rb: number, h: number, seg: number, x: number, y: number, z: number, color: THREE.ColorRepresentation, rot?: [number, number, number]): this {
    return this.add(key, new THREE.CylinderGeometry(rt, rb, h, seg), color, [x, y, z], rot);
  }

  /** Wheel lying on its side (axis along X). */
  wheel(key: string, r: number, w: number, x: number, y: number, z: number, color: THREE.ColorRepresentation, seg = 12): this {
    return this.cyl(key, r, r, w, seg, x, y, z, color, [0, 0, Math.PI / 2]);
  }

  sphere(key: string, r: number, x: number, y: number, z: number, color: THREE.ColorRepresentation, detail = 1, scale?: [number, number, number]): this {
    return this.add(key, new THREE.IcosahedronGeometry(r, detail), color, [x, y, z], undefined, scale);
  }

  geometry(key: string): THREE.BufferGeometry | null {
    const list = this.parts.get(key);
    if (!list || !list.length) return null;
    const merged = mergeGeometries(list, false);
    merged.computeBoundingSphere();
    return merged;
  }

  build(materials: Record<string, THREE.Material>, opts: { castShadow?: boolean; receiveShadow?: boolean } = {}): THREE.Group {
    const group = new THREE.Group();
    for (const key of this.parts.keys()) {
      const g = this.geometry(key);
      const mat = materials[key];
      if (!g || !mat) continue;
      const mesh = new THREE.Mesh(g, mat);
      mesh.name = key;
      mesh.castShadow = !!opts.castShadow && key !== 'glow';
      mesh.receiveShadow = !!opts.receiveShadow;
      group.add(mesh);
    }
    return group;
  }
}
