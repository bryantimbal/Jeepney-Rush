import * as THREE from 'three';
import { curve } from '../render/Curve';

interface Particle {
  x: number; y: number; z: number;
  vx: number; vy: number; vz: number;
  life: number; max: number;
  size: number; grow: number;
  gravity: number;
  r: number; g: number; b: number;
  scroll: boolean;
}

/**
 * One InstancedMesh for every particle in the game (sparks, smoke, coin
 * sparkles, confetti) = a single draw call.
 */
export class Effects {
  private mesh: THREE.InstancedMesh;
  private parts: Particle[] = [];
  private readonly MAX = 400;
  private m4 = new THREE.Matrix4();
  private q = new THREE.Quaternion();
  private v = new THREE.Vector3();
  private s = new THREE.Vector3();
  private c = new THREE.Color();
  private e = new THREE.Euler();
  private speedLines: THREE.InstancedMesh;
  private lines: { x: number; y: number; z: number; len: number }[] = [];
  private lineOpacity = 0;

  constructor(scene: THREE.Scene) {
    const geo = new THREE.BoxGeometry(1, 1, 1);
    const mat = curve(new THREE.MeshBasicMaterial({ color: '#ffffff', transparent: true, opacity: 0.95, depthWrite: false, toneMapped: false }));
    this.mesh = new THREE.InstancedMesh(geo, mat, this.MAX);
    this.mesh.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
    this.mesh.count = 0;
    this.mesh.frustumCulled = false;
    this.mesh.setColorAt(0, new THREE.Color());
    scene.add(this.mesh);

    const lgeo = new THREE.BoxGeometry(0.04, 0.04, 1);
    const lmat = new THREE.MeshBasicMaterial({ color: '#ffffff', transparent: true, opacity: 0, depthWrite: false });
    this.speedLines = new THREE.InstancedMesh(lgeo, lmat, 40);
    this.speedLines.frustumCulled = false;
    for (let i = 0; i < 40; i++) this.lines.push(this.newLine(true));
    scene.add(this.speedLines);
  }

  private newLine(anyZ: boolean): { x: number; y: number; z: number; len: number } {
    const side = Math.random() < 0.5 ? -1 : 1;
    return { x: side * (2.5 + Math.random() * 5), y: 0.5 + Math.random() * 6, z: anyZ ? -Math.random() * 60 : -60 - Math.random() * 10, len: 3 + Math.random() * 5 };
  }

  emit(n: number, o: Partial<Particle> & { x: number; y: number; z: number }, spread = 1, color: THREE.ColorRepresentation = '#fff', speed = 4): void {
    this.c.set(color);
    for (let i = 0; i < n && this.parts.length < this.MAX; i++) {
      const max = (o.max ?? 0.7) * (0.6 + Math.random() * 0.6);
      this.parts.push({
        x: o.x + (Math.random() - 0.5) * spread,
        y: o.y + (Math.random() - 0.5) * spread * 0.5,
        z: o.z + (Math.random() - 0.5) * spread,
        vx: (o.vx ?? 0) + (Math.random() - 0.5) * speed,
        vy: (o.vy ?? 0) + Math.random() * speed,
        vz: (o.vz ?? 0) + (Math.random() - 0.5) * speed,
        life: max,
        max,
        size: (o.size ?? 0.15) * (0.6 + Math.random() * 0.8),
        grow: o.grow ?? 0,
        gravity: o.gravity ?? 12,
        r: this.c.r, g: this.c.g, b: this.c.b,
        scroll: o.scroll ?? true,
      });
    }
  }

  sparks(x: number, y: number, z: number): void {
    this.emit(26, { x, y, z, size: 0.12, max: 0.5, gravity: 18 }, 0.8, '#ffd166', 9);
    this.emit(14, { x, y, z, size: 0.5, grow: 2.2, max: 0.9, gravity: -1.5 }, 1.2, '#6c6c6c', 2);
  }

  coinSparkle(x: number, y: number, z: number): void {
    this.emit(8, { x, y, z, size: 0.12, max: 0.35, gravity: 4 }, 0.5, '#fff3a0', 4);
  }

  confetti(x: number, y: number, z: number, n = 30): void {
    const cols = ['#ce1126', '#fcd116', '#0038a8', '#ffffff', '#06d6a0', '#ff8c42'];
    for (let i = 0; i < n; i++) this.emit(1, { x, y, z, size: 0.14, max: 1.1, gravity: 9, vy: 4 }, 1.4, cols[i % cols.length], 7);
  }

  exhaust(x: number, y: number, z: number, heavy: boolean): void {
    this.emit(1, { x, y, z, size: heavy ? 0.22 : 0.15, grow: 0.55, max: heavy ? 0.7 : 0.5, gravity: -1.5, vz: 2 }, 0.1, heavy ? '#5a5a5a' : '#a8a8a8', 0.5);
  }

  dust(x: number, z: number): void {
    this.emit(10, { x, y: 0.15, z, size: 0.35, grow: 1.5, max: 0.6, gravity: -0.5 }, 1.6, '#c9b79c', 2);
  }

  clear(): void {
    this.parts.length = 0;
    this.mesh.count = 0;
  }

  update(dt: number, worldSpeed: number, speedFactor: number): void {
    let n = 0;
    for (let i = this.parts.length - 1; i >= 0; i--) {
      const p = this.parts[i];
      p.life -= dt;
      if (p.life <= 0) {
        this.parts[i] = this.parts[this.parts.length - 1];
        this.parts.pop();
        continue;
      }
      p.vy -= p.gravity * dt;
      p.x += p.vx * dt;
      p.y = Math.max(0.02, p.y + p.vy * dt);
      p.z += (p.vz + (p.scroll ? worldSpeed : 0)) * dt;
      p.size += p.grow * dt;
      const k = p.life / p.max;
      const sz = p.size * (0.3 + 0.7 * k);
      this.v.set(p.x, p.y, p.z);
      this.s.set(sz, sz, sz);
      this.e.set(p.life * 6, p.life * 4, 0);
      this.q.setFromEuler(this.e);
      this.m4.compose(this.v, this.q, this.s);
      this.mesh.setMatrixAt(n, this.m4);
      this.c.setRGB(p.r, p.g, p.b);
      this.mesh.setColorAt(n, this.c);
      n++;
    }
    this.mesh.count = n;
    this.mesh.instanceMatrix.needsUpdate = true;
    if (this.mesh.instanceColor) this.mesh.instanceColor.needsUpdate = true;

    // speed lines fade in at high speed / gas
    const targetOp = Math.max(0, speedFactor - 0.45) * 0.9;
    this.lineOpacity += (targetOp - this.lineOpacity) * Math.min(1, dt * 4);
    (this.speedLines.material as THREE.MeshBasicMaterial).opacity = this.lineOpacity * 0.5;
    this.speedLines.visible = this.lineOpacity > 0.02;
    if (this.speedLines.visible) {
      for (let i = 0; i < this.lines.length; i++) {
        const l = this.lines[i];
        l.z += worldSpeed * 2.2 * dt;
        if (l.z > 12) this.lines[i] = this.newLine(false);
        const L = this.lines[i];
        this.v.set(L.x, L.y, L.z);
        this.s.set(1, 1, L.len * (0.5 + speedFactor));
        this.q.identity();
        this.m4.compose(this.v, this.q, this.s);
        this.speedLines.setMatrixAt(i, this.m4);
      }
      this.speedLines.instanceMatrix.needsUpdate = true;
    }
  }
}
