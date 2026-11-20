import * as THREE from 'three';

/**
 * All textures are generated on canvas at load time: zero external image
 * assets, tiny APK, crisp at any resolution, and trivially re-skinnable.
 */
export const FONT_DISPLAY = '"Luckiest Guy", "Arial Black", Impact, sans-serif';
export const FONT_BODY = '"Nunito", "Segoe UI", Roboto, sans-serif';

function canvas(w: number, h: number): [HTMLCanvasElement, CanvasRenderingContext2D] {
  const c = document.createElement('canvas');
  c.width = w;
  c.height = h;
  return [c, c.getContext('2d')!];
}

function tex(c: HTMLCanvasElement, repeat = false): THREE.CanvasTexture {
  const t = new THREE.CanvasTexture(c);
  t.colorSpace = THREE.SRGBColorSpace;
  t.anisotropy = 4;
  if (repeat) t.wrapS = t.wrapT = THREE.RepeatWrapping;
  return t;
}

function noise(ctx: CanvasRenderingContext2D, w: number, h: number, n: number, alpha: number, light = false): void {
  for (let i = 0; i < n; i++) {
    const v = Math.floor(Math.random() * 60) + (light ? 170 : 20);
    ctx.fillStyle = `rgba(${v},${v},${v},${alpha * Math.random()})`;
    ctx.fillRect(Math.random() * w, Math.random() * h, 1 + Math.random() * 2, 1 + Math.random() * 2);
  }
}

/** Road: 3 lanes, dashed white dividers, yellow edges. Texture spans 12 m. */
export const ROAD_TEX_METRES = 12;
export function roadTexture(roadWidth: number, laneOffsets: readonly number[]): THREE.CanvasTexture {
  const W = 512, H = 512;
  const [c, ctx] = canvas(W, H);
  ctx.fillStyle = '#44464d';
  ctx.fillRect(0, 0, W, H);
  noise(ctx, W, H, 9000, 0.35);
  noise(ctx, W, H, 2500, 0.15, true);
  // patches & oil stains
  for (let i = 0; i < 4; i++) {
    ctx.fillStyle = `rgba(20,20,24,${0.12 + Math.random() * 0.1})`;
    ctx.beginPath();
    ctx.ellipse(Math.random() * W, Math.random() * H, 20 + Math.random() * 40, 30 + Math.random() * 60, Math.random(), 0, Math.PI * 2);
    ctx.fill();
  }
  const px = (x: number) => W / 2 + (x / roadWidth) * W;
  // tyre tracks (darker) in each lane
  for (const lx of laneOffsets) {
    ctx.fillStyle = 'rgba(25,25,30,0.18)';
    ctx.fillRect(px(lx - 0.75) - 10, 0, 20, H);
    ctx.fillRect(px(lx + 0.75) - 10, 0, 20, H);
  }
  // lane dividers: 3 m dash, 3 m gap -> two dashes per 12 m
  ctx.fillStyle = '#f2f2ee';
  for (let i = 0; i < laneOffsets.length - 1; i++) {
    const x = px((laneOffsets[i] + laneOffsets[i + 1]) / 2);
    for (let d = 0; d < 2; d++) ctx.fillRect(x - 5, (d * H) / 2 + H / 8, 10, H / 4);
  }
  // edge lines
  ctx.fillStyle = '#f5c518';
  ctx.fillRect(px(-roadWidth / 2 + 0.25) - 6, 0, 12, H);
  ctx.fillRect(px(roadWidth / 2 - 0.25) - 6, 0, 12, H);
  const t = tex(c, true);
  t.anisotropy = 8;
  return t;
}

/** Concrete sidewalk tiles, 2 m per texture tile. */
export function sidewalkTexture(): THREE.CanvasTexture {
  const [c, ctx] = canvas(256, 256);
  ctx.fillStyle = '#b9b2a6';
  ctx.fillRect(0, 0, 256, 256);
  noise(ctx, 256, 256, 3000, 0.25);
  ctx.strokeStyle = 'rgba(80,70,60,0.45)';
  ctx.lineWidth = 3;
  for (let i = 0; i <= 2; i++) {
    ctx.beginPath();
    ctx.moveTo(0, i * 128);
    ctx.lineTo(256, i * 128);
    ctx.moveTo(i * 128, 0);
    ctx.lineTo(i * 128, 256);
    ctx.stroke();
  }
  return tex(c, true);
}

/** Manila-style curb painted in alternating yellow and black. 4 m per tile. */
export function curbTexture(): THREE.CanvasTexture {
  const [c, ctx] = canvas(256, 32);
  for (let i = 0; i < 4; i++) {
    ctx.fillStyle = i % 2 ? '#1d1d1f' : '#f2c21b';
    ctx.fillRect(i * 64, 0, 64, 32);
  }
  noise(ctx, 256, 32, 300, 0.3);
  return tex(c, true);
}

/** White facade with windows; tinted by vertex colour. One tile = 3 m x 3 m. */
export function facadeTexture(): THREE.CanvasTexture {
  const S = 256;
  const [c, ctx] = canvas(S * 2, S * 2);
  ctx.fillStyle = '#ffffff';
  ctx.fillRect(0, 0, S * 2, S * 2);
  noise(ctx, S * 2, S * 2, 4000, 0.12);
  for (let gx = 0; gx < 2; gx++)
    for (let gy = 0; gy < 2; gy++) {
      const ox = gx * S, oy = gy * S;
      const variant = gx + gy * 2;
      ctx.fillStyle = '#d9d9d9';
      ctx.fillRect(ox + 40, oy + 50, S - 80, S - 90); // frame
      if (variant === 3) {
        // rolled shutter
        ctx.fillStyle = '#8f949a';
        ctx.fillRect(ox + 48, oy + 58, S - 96, S - 106);
        ctx.fillStyle = 'rgba(0,0,0,0.25)';
        for (let y = oy + 60; y < oy + S - 50; y += 8) ctx.fillRect(ox + 48, y, S - 96, 2);
      } else {
        const g = ctx.createLinearGradient(0, oy + 58, 0, oy + S - 48);
        g.addColorStop(0, '#2b3d55');
        g.addColorStop(1, '#5a7898');
        ctx.fillStyle = g;
        ctx.fillRect(ox + 48, oy + 58, S - 96, S - 106);
        ctx.fillStyle = 'rgba(255,255,255,0.25)';
        ctx.fillRect(ox + S / 2 - 3, oy + 58, 6, S - 106);
        ctx.fillRect(ox + 48, oy + 110, S - 96, 5);
        // window grills (very common in Manila)
        ctx.strokeStyle = 'rgba(30,30,30,0.55)';
        ctx.lineWidth = 3;
        for (let x = ox + 60; x < ox + S - 48; x += 18) {
          ctx.beginPath();
          ctx.moveTo(x, oy + 58);
          ctx.lineTo(x, oy + S - 48);
          ctx.stroke();
        }
      }
      if (variant === 1) {
        // aircon unit
        ctx.fillStyle = '#e8e8e8';
        ctx.fillRect(ox + 150, oy + 170, 60, 40);
        ctx.fillStyle = '#9a9a9a';
        for (let y = oy + 176; y < oy + 206; y += 6) ctx.fillRect(ox + 156, y, 48, 2);
      }
      if (variant === 2) {
        // laundry line
        ctx.strokeStyle = '#444';
        ctx.lineWidth = 2;
        ctx.beginPath();
        ctx.moveTo(ox + 40, oy + 215);
        ctx.lineTo(ox + S - 40, oy + 215);
        ctx.stroke();
        const cols = ['#e63946', '#2a9d8f', '#f4a261', '#457b9d', '#ffd166'];
        for (let i = 0; i < 5; i++) {
          ctx.fillStyle = cols[i];
          ctx.fillRect(ox + 55 + i * 30, oy + 215, 20, 26);
        }
      }
      // floor ledge
      ctx.fillStyle = 'rgba(0,0,0,0.18)';
      ctx.fillRect(ox, oy + S - 14, S, 14);
    }
  return tex(c, true);
}

export interface SignSpec {
  text: string;
  sub?: string;
  bg: string;
  fg: string;
  accent?: string;
}

export const SHOP_SIGNS: SignSpec[] = [
  { text: 'SARI-SARI STORE', sub: 'Ice • Load • Softdrinks', bg: '#e63946', fg: '#fff', accent: '#ffd166' },
  { text: 'CARINDERIA', sub: 'Lutong Bahay', bg: '#ffd166', fg: '#7a1f1f', accent: '#e63946' },
  { text: 'LOAD NA DITO!', sub: 'E-Load • Pasaload', bg: '#06d6a0', fg: '#073b4c', accent: '#fff' },
  { text: 'PANADERIA', sub: 'Mainit na Pandesal', bg: '#f4a261', fg: '#3d1f00', accent: '#fff3d6' },
  { text: 'VULCANIZING', sub: '24 Hours', bg: '#1d3557', fg: '#ffd166', accent: '#e63946' },
  { text: 'BOTIKA', sub: 'Generics • Vitamins', bg: '#2a9d8f', fg: '#fff', accent: '#e9f5db' },
  { text: 'LECHON MANOK', sub: 'Inasal • Liempo', bg: '#9d0208', fg: '#ffba08', accent: '#ffba08' },
  { text: 'HALO-HALO', sub: 'Mais con Yelo', bg: '#9b5de5', fg: '#fff', accent: '#fee440' },
  { text: 'BARBERSHOP', sub: 'Gupit ₱80', bg: '#f1faee', fg: '#1d3557', accent: '#e63946' },
  { text: 'KAIN NA!', sub: 'Silog Meals ₱99', bg: '#ff7b00', fg: '#fff', accent: '#ffd000' },
  { text: 'PANSITAN', sub: 'Canton • Bihon', bg: '#ffbe0b', fg: '#3a0ca3', accent: '#fb5607' },
  { text: 'TERMINAL', sub: 'Jeepney Terminal', bg: '#264653', fg: '#e9c46a', accent: '#e76f51' },
  { text: 'FIESTA SALE!', sub: 'Lahat Mura!', bg: '#ef476f', fg: '#fff', accent: '#ffd166' },
  { text: 'TAHO', sub: 'Arnibal • Sago', bg: '#bc6c25', fg: '#fefae0', accent: '#dda15e' },
  { text: 'MERIENDA', sub: 'Turon • Banana Cue', bg: '#ffd60a', fg: '#003566', accent: '#ffc300' },
  { text: 'INTERNET CAFE', sub: '₱15 / hour', bg: '#3a86ff', fg: '#fff', accent: '#8338ec' },
];

/** Atlas of shop signs: 4 columns x 4 rows, each cell 256 x 96 px. */
export const SIGN_COLS = 4;
export const SIGN_ROWS = 4;
export function signAtlas(): THREE.CanvasTexture {
  const CW = 256, CH = 96;
  const [c, ctx] = canvas(CW * SIGN_COLS, CH * SIGN_ROWS);
  SHOP_SIGNS.forEach((s, i) => {
    const x = (i % SIGN_COLS) * CW, y = Math.floor(i / SIGN_COLS) * CH;
    ctx.fillStyle = s.bg;
    ctx.fillRect(x, y, CW, CH);
    ctx.strokeStyle = s.accent ?? '#fff';
    ctx.lineWidth = 6;
    ctx.strokeRect(x + 5, y + 5, CW - 10, CH - 10);
    ctx.fillStyle = s.fg;
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    fitText(ctx, s.text, x + CW / 2, y + CH * 0.42, CW - 30, 40, FONT_DISPLAY);
    if (s.sub) {
      ctx.fillStyle = s.accent ?? s.fg;
      fitText(ctx, s.sub, x + CW / 2, y + CH * 0.78, CW - 40, 17, `800 ${''}` + FONT_BODY, true);
    }
  });
  return tex(c);
}

export function signUV(index: number): [number, number, number, number] {
  const col = index % SIGN_COLS, row = Math.floor(index / SIGN_COLS);
  // canvas y=0 is top; texture v=1 is top (flipY)
  const u0 = col / SIGN_COLS, u1 = (col + 1) / SIGN_COLS;
  const v1 = 1 - row / SIGN_ROWS, v0 = 1 - (row + 1) / SIGN_ROWS;
  return [u0, v0, u1, v1];
}

export function fitText(ctx: CanvasRenderingContext2D, text: string, x: number, y: number, maxW: number, size: number, font: string, weightIncluded = false): void {
  let s = size;
  do {
    ctx.font = weightIncluded ? font.replace(/^(\d+)\s*/, `$1 ${s}px `) : `${s}px ${font}`;
    if (ctx.measureText(text).width <= maxW) break;
    s -= 2;
  } while (s > 8);
  ctx.fillText(text, x, y);
}

/** Road marking for passenger stop zones. Drawn once, shared by all stops. */
export function stopZoneTexture(): THREE.CanvasTexture {
  const [c, ctx] = canvas(128, 512);
  ctx.clearRect(0, 0, 128, 512);
  ctx.fillStyle = 'rgba(255, 209, 0, 0.30)';
  ctx.fillRect(0, 0, 128, 512);
  ctx.strokeStyle = '#ffd100';
  ctx.lineWidth = 10;
  ctx.strokeRect(5, 5, 118, 502);
  // diagonal hatch
  ctx.strokeStyle = 'rgba(255, 209, 0, 0.6)';
  ctx.lineWidth = 6;
  for (let y = -128; y < 512; y += 40) {
    ctx.beginPath();
    ctx.moveTo(0, y);
    ctx.lineTo(128, y + 128);
    ctx.stroke();
  }
  ctx.save();
  ctx.translate(64, 256);
  ctx.rotate(-Math.PI / 2);
  ctx.fillStyle = '#ffffff';
  ctx.strokeStyle = '#7a4b00';
  ctx.lineWidth = 6;
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.font = `64px ${FONT_DISPLAY}`;
  ctx.strokeText('PARA', 0, 4);
  ctx.fillText('PARA', 0, 4);
  ctx.restore();
  return tex(c);
}

/** Per-stop sign showing the stop name (re-drawn when a pooled stop is reused). */
export function drawStopSign(c: HTMLCanvasElement, name: string): void {
  const ctx = c.getContext('2d')!;
  const W = c.width, H = c.height;
  ctx.fillStyle = '#0b6e4f';
  ctx.fillRect(0, 0, W, H);
  ctx.strokeStyle = '#fff';
  ctx.lineWidth = 6;
  ctx.strokeRect(6, 6, W - 12, H - 12);
  ctx.fillStyle = '#ffd100';
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  fitText(ctx, 'SAKAYAN / BABAAN', W / 2, H * 0.27, W - 30, 22, `800 ` + FONT_BODY, true);
  ctx.fillStyle = '#fff';
  fitText(ctx, name, W / 2, H * 0.65, W - 30, 52, FONT_DISPLAY);
}

export function newCanvas(w: number, h: number): HTMLCanvasElement {
  return canvas(w, h)[0];
}

export function canvasTexture(c: HTMLCanvasElement): THREE.CanvasTexture {
  return tex(c);
}

/** Sky gradient used as scene background. */
export function skyTexture(top: string, mid: string, bottom: string): THREE.CanvasTexture {
  const [c, ctx] = canvas(4, 256);
  const g = ctx.createLinearGradient(0, 0, 0, 256);
  g.addColorStop(0, top);
  g.addColorStop(0.55, mid);
  g.addColorStop(1, bottom);
  ctx.fillStyle = g;
  ctx.fillRect(0, 0, 4, 256);
  return tex(c);
}

/** Soft round sprite for particles / glows. */
export function softDotTexture(): THREE.CanvasTexture {
  const [c, ctx] = canvas(64, 64);
  const g = ctx.createRadialGradient(32, 32, 0, 32, 32, 32);
  g.addColorStop(0, 'rgba(255,255,255,1)');
  g.addColorStop(0.4, 'rgba(255,255,255,0.6)');
  g.addColorStop(1, 'rgba(255,255,255,0)');
  ctx.fillStyle = g;
  ctx.fillRect(0, 0, 64, 64);
  return tex(c);
}

/** Dark radial blob used as a cheap contact shadow under vehicles. */
export function blobShadowTexture(): THREE.CanvasTexture {
  const [c, ctx] = canvas(64, 128);
  ctx.save();
  ctx.scale(1, 2);
  const g = ctx.createRadialGradient(32, 32, 2, 32, 32, 32);
  g.addColorStop(0, 'rgba(0,0,0,0.55)');
  g.addColorStop(0.6, 'rgba(0,0,0,0.3)');
  g.addColorStop(1, 'rgba(0,0,0,0)');
  ctx.fillStyle = g;
  ctx.fillRect(0, 0, 64, 64);
  ctx.restore();
  return tex(c);
}
