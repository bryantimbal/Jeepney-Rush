import * as THREE from 'three';
import { curveUniforms } from './Curve';
import { skyTexture } from '../art/Textures';

export type Quality = 'low' | 'medium' | 'high';

/** Owns the WebGL renderer, scene, camera rig and lighting. */
export class Renderer {
  readonly renderer: THREE.WebGLRenderer;
  readonly scene = new THREE.Scene();
  readonly camera: THREE.PerspectiveCamera;
  readonly sun: THREE.DirectionalLight;
  readonly hemi: THREE.HemisphereLight;
  quality: Quality = 'high';
  private shake = 0;
  private shakeT = 0;
  private baseFov = 62;
  camTarget = new THREE.Vector3();
  camPos = new THREE.Vector3(0, 7.4, 12.5);
  lookAhead = new THREE.Vector3(0, 0.2, -16);

  constructor(private container: HTMLElement) {
    this.renderer = new THREE.WebGLRenderer({ antialias: true, powerPreference: 'high-performance', stencil: false });
    this.renderer.outputColorSpace = THREE.SRGBColorSpace;
    this.renderer.toneMapping = THREE.ACESFilmicToneMapping;
    this.renderer.toneMappingExposure = 1.05;
    this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    container.appendChild(this.renderer.domElement);

    this.camera = new THREE.PerspectiveCamera(this.baseFov, 9 / 16, 0.3, 420);
    this.camera.position.copy(this.camPos);

    this.scene.background = skyTexture('#3a8dde', '#9fd3f7', '#ffe3b8');
    this.scene.fog = new THREE.Fog('#f6dfc0', 70, 210);

    this.hemi = new THREE.HemisphereLight('#dff2ff', '#7a6a55', 1.25);
    this.scene.add(this.hemi);
    this.sun = new THREE.DirectionalLight('#fff1d6', 2.3);
    this.sun.position.set(-9, 18, 6);
    this.sun.target.position.set(0, 0, -8);
    this.sun.shadow.mapSize.set(1024, 1024);
    const sc = this.sun.shadow.camera;
    sc.left = -12;
    sc.right = 12;
    sc.top = 26;
    sc.bottom = -16;
    sc.near = 1;
    sc.far = 60;
    this.sun.shadow.bias = -0.0008;
    this.sun.shadow.normalBias = 0.02;
    this.scene.add(this.sun, this.sun.target);

    window.addEventListener('resize', () => this.resize());
    this.resize();
  }

  setQuality(q: Quality): void {
    this.quality = q;
    const dpr = Math.min(window.devicePixelRatio || 1, q === 'high' ? 2 : q === 'medium' ? 1.5 : 1);
    this.renderer.setPixelRatio(dpr);
    const shadows = q !== 'low';
    this.renderer.shadowMap.enabled = shadows;
    this.sun.castShadow = shadows;
    this.sun.shadow.mapSize.set(q === 'high' ? 2048 : 1024, q === 'high' ? 2048 : 1024);
    this.sun.shadow.map?.dispose();
    (this.sun.shadow as { map: THREE.WebGLRenderTarget | null }).map = null;
    this.scene.traverse((o) => {
      const m = (o as THREE.Mesh).material as THREE.Material | THREE.Material[] | undefined;
      if (m) (Array.isArray(m) ? m : [m]).forEach((mm) => (mm.needsUpdate = true));
    });
    this.resize();
  }

  resize(): void {
    const w = this.container.clientWidth || window.innerWidth;
    const h = this.container.clientHeight || window.innerHeight;
    this.renderer.setSize(w, h, false);
    this.renderer.domElement.style.width = w + 'px';
    this.renderer.domElement.style.height = h + 'px';
    this.camera.aspect = w / h;
    // Keep all three lanes visible on narrow portrait phones and wide tablets.
    const aspect = w / h;
    this.baseFov = aspect < 0.5 ? 70 : aspect < 0.62 ? 64 : aspect < 1 ? 58 : 50;
    this.camera.fov = this.baseFov;
    this.camera.updateProjectionMatrix();
  }

  addShake(amount: number): void {
    this.shake = Math.max(this.shake, amount);
  }

  /** Smooth chase camera following the player's lateral position and jump. */
  updateCamera(dt: number, playerX: number, playerY: number, speedFactor: number): void {
    this.shakeT += dt;
    const targetX = playerX * 0.55;
    this.camPos.x += (targetX - this.camPos.x) * Math.min(1, dt * 5);
    const camY = 7.4 + playerY * 0.35;
    this.camPos.y += (camY - this.camPos.y) * Math.min(1, dt * 4);
    this.camera.position.copy(this.camPos);
    if (this.shake > 0.001) {
      this.camera.position.x += Math.sin(this.shakeT * 61) * this.shake;
      this.camera.position.y += Math.cos(this.shakeT * 47) * this.shake * 0.7;
      this.shake *= Math.pow(0.02, dt);
    }
    this.camTarget.set(playerX * 0.8, this.lookAhead.y + playerY * 0.25, this.lookAhead.z);
    this.camera.lookAt(this.camTarget);
    const fov = this.baseFov + speedFactor * 9;
    if (Math.abs(this.camera.fov - fov) > 0.05) {
      this.camera.fov += (fov - this.camera.fov) * Math.min(1, dt * 3);
      this.camera.updateProjectionMatrix();
    }
  }

  setRoadBend(x: number): void {
    curveUniforms.uCurveX.value = x;
  }

  render(): void {
    this.renderer.render(this.scene, this.camera);
  }
}
