import * as THREE from 'three';
import { RoomEnvironment } from 'three/examples/jsm/environments/RoomEnvironment.js';
import { curve } from '../render/Curve';
import * as T from './Textures';

/** Shared materials. Created once after the renderer exists. */
export class Materials {
  vc!: THREE.MeshLambertMaterial;
  glow!: THREE.MeshBasicMaterial;
  chrome!: THREE.MeshStandardMaterial;
  glass!: THREE.MeshStandardMaterial;
  paintVC!: THREE.MeshStandardMaterial;
  facade!: THREE.MeshLambertMaterial;
  signs!: THREE.MeshBasicMaterial;
  road!: THREE.MeshLambertMaterial;
  sidewalk!: THREE.MeshLambertMaterial;
  curb!: THREE.MeshLambertMaterial;
  zone!: THREE.MeshBasicMaterial;
  blob!: THREE.MeshBasicMaterial;
  envMap!: THREE.Texture;
  roadTex!: THREE.CanvasTexture;
  sidewalkTex!: THREE.CanvasTexture;
  curbTex!: THREE.CanvasTexture;

  init(renderer: THREE.WebGLRenderer, roadWidth: number, lanes: readonly number[]): void {
    const pmrem = new THREE.PMREMGenerator(renderer);
    this.envMap = pmrem.fromScene(new RoomEnvironment(), 0.04).texture;
    pmrem.dispose();

    this.vc = curve(new THREE.MeshLambertMaterial({ vertexColors: true }));
    this.glow = curve(new THREE.MeshBasicMaterial({ vertexColors: true, toneMapped: false }));
    this.chrome = curve(new THREE.MeshStandardMaterial({ vertexColors: true, metalness: 1, roughness: 0.18, envMap: this.envMap, envMapIntensity: 1.25 }));
    this.glass = curve(new THREE.MeshStandardMaterial({ vertexColors: true, metalness: 0.4, roughness: 0.08, envMap: this.envMap, envMapIntensity: 0.9 }));
    this.paintVC = curve(new THREE.MeshStandardMaterial({ vertexColors: true, metalness: 0.15, roughness: 0.38, envMap: this.envMap, envMapIntensity: 0.6 }));

    const facade = T.facadeTexture();
    this.facade = curve(new THREE.MeshLambertMaterial({ vertexColors: true, map: facade }));
    this.signs = curve(new THREE.MeshBasicMaterial({ map: T.signAtlas() }));

    this.roadTex = T.roadTexture(roadWidth, lanes);
    this.road = curve(new THREE.MeshLambertMaterial({ map: this.roadTex }));
    this.sidewalkTex = T.sidewalkTexture();
    this.sidewalk = curve(new THREE.MeshLambertMaterial({ map: this.sidewalkTex }));
    this.curbTex = T.curbTexture();
    this.curb = curve(new THREE.MeshLambertMaterial({ map: this.curbTex }));
    this.zone = curve(new THREE.MeshBasicMaterial({ map: T.stopZoneTexture(), transparent: true, depthWrite: false, polygonOffset: true, polygonOffsetFactor: -2 }));
    this.blob = curve(new THREE.MeshBasicMaterial({ map: T.blobShadowTexture(), transparent: true, depthWrite: false, polygonOffset: true, polygonOffsetFactor: -1 }));
  }

  /** Map key -> material, used by ModelKit.build */
  get kit(): Record<string, THREE.Material> {
    return { vc: this.vc, glow: this.glow, chrome: this.chrome, glass: this.glass, paint: this.paintVC, facade: this.facade };
  }
}

export const materials = new Materials();
