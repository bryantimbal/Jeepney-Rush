import * as THREE from 'three';

/**
 * "Bent world" vertex shader trick used by many commercial runners: geometry
 * ahead of the player is pushed down (horizon roll-off) and sideways (gentle
 * road bends). Purely visual – gameplay collision stays in flat space.
 */
export const curveUniforms = {
  uCurveY: { value: 0.0016 },
  uCurveX: { value: 0.0 },
};

const patched = new WeakSet<THREE.Material>();

export function curve<T extends THREE.Material>(mat: T): T {
  if (patched.has(mat)) return mat;
  patched.add(mat);
  const prev = mat.onBeforeCompile;
  mat.onBeforeCompile = (shader, renderer) => {
    prev?.call(mat, shader, renderer);
    shader.uniforms.uCurveY = curveUniforms.uCurveY;
    shader.uniforms.uCurveX = curveUniforms.uCurveX;
    shader.vertexShader = shader.vertexShader
      .replace('#include <common>', '#include <common>\nuniform float uCurveY;\nuniform float uCurveX;')
      .replace(
        '#include <project_vertex>',
        `vec4 mvPosition = vec4( transformed, 1.0 );
#ifdef USE_BATCHING
  mvPosition = batchingMatrix * mvPosition;
#endif
#ifdef USE_INSTANCING
  mvPosition = instanceMatrix * mvPosition;
#endif
vec4 jrWorld = modelMatrix * mvPosition;
float jrZ = min( jrWorld.z, 0.0 );
jrWorld.y -= uCurveY * jrZ * jrZ;
jrWorld.x += uCurveX * jrZ * jrZ;
mvPosition = viewMatrix * jrWorld;
gl_Position = projectionMatrix * mvPosition;`,
      );
  };
  mat.customProgramCacheKey = () => 'jrcurve';
  return mat;
}
