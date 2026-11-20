import * as THREE from 'three';
import { FONT_DISPLAY, FONT_BODY, fitText, newCanvas, canvasTexture } from './Textures';

export interface JeepneySkin {
  id: string;
  name: string;
  body: string; // main body (silver for stainless jeepneys)
  stripes: string[];
  hood: string;
  roof: string;
  trim: string;
  slogan: string;
  rearText: string;
  route: string;
  plate: string;
}

/** Phase 1 ships with the classic skin; more skins plug into the Phase 2 garage. */
export const CLASSIC_SKIN: JeepneySkin = {
  id: 'classic',
  name: 'Classic Pinoy',
  body: '#d7dbe2',
  stripes: ['#ce1126', '#fcd116', '#0038a8', '#ffffff'],
  hood: '#ce1126',
  roof: '#0038a8',
  trim: '#fcd116',
  slogan: 'BIYAHENG PINOY',
  rearText: 'GOD BLESS OUR TRIP',
  route: 'QUIAPO ⇄ CUBAO',
  plate: 'PUJ 2026',
};

function brushed(ctx: CanvasRenderingContext2D, w: number, h: number, base: string): void {
  const g = ctx.createLinearGradient(0, 0, 0, h);
  const c = new THREE.Color(base);
  const lighter = c.clone().lerp(new THREE.Color('#ffffff'), 0.45).getStyle();
  const darker = c.clone().multiplyScalar(0.72).getStyle();
  g.addColorStop(0, lighter);
  g.addColorStop(0.45, base);
  g.addColorStop(1, darker);
  ctx.fillStyle = g;
  ctx.fillRect(0, 0, w, h);
  for (let i = 0; i < 260; i++) {
    ctx.fillStyle = `rgba(255,255,255,${Math.random() * 0.08})`;
    ctx.fillRect(0, Math.random() * h, w, 1);
  }
}

function star(ctx: CanvasRenderingContext2D, x: number, y: number, r: number, color: string): void {
  ctx.fillStyle = color;
  ctx.beginPath();
  for (let i = 0; i < 10; i++) {
    const a = (i / 10) * Math.PI * 2 - Math.PI / 2;
    const rr = i % 2 ? r * 0.45 : r;
    ctx.lineTo(x + Math.cos(a) * rr, y + Math.sin(a) * rr);
  }
  ctx.fill();
}

/** Side panel art: brushed steel, flowing stripes, slogan, route. 1024x256 */
export function jeepneySideTexture(skin: JeepneySkin): THREE.CanvasTexture {
  const W = 1024, H = 256;
  const c = newCanvas(W, H);
  const ctx = c.getContext('2d')!;
  brushed(ctx, W, H, skin.body);
  // flowing wave stripes
  skin.stripes.forEach((col, i) => {
    ctx.strokeStyle = col;
    ctx.lineWidth = 14 - i * 2;
    ctx.beginPath();
    for (let x = 0; x <= W; x += 8) {
      const y = H * 0.74 + i * 13 + Math.sin(x / 70 + i * 0.6) * 10;
      x === 0 ? ctx.moveTo(x, y) : ctx.lineTo(x, y);
    }
    ctx.stroke();
  });
  // chrome rub rail
  const rail = ctx.createLinearGradient(0, H - 26, 0, H);
  rail.addColorStop(0, '#ffffff');
  rail.addColorStop(0.5, '#8a8f99');
  rail.addColorStop(1, '#e8ebf0');
  ctx.fillStyle = rail;
  ctx.fillRect(0, H - 20, W, 20);
  // panel rivets / seams
  ctx.fillStyle = 'rgba(0,0,0,0.25)';
  for (let x = 128; x < W; x += 128) ctx.fillRect(x, 0, 2, H - 20);
  // emblem flames near the front & back
  for (const fx of [70, W - 70]) {
    for (let k = 0; k < 3; k++) star(ctx, fx, 70 + k * 46, 14 - k * 2, skin.stripes[k % skin.stripes.length]);
  }
  // slogan
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.lineJoin = 'round';
  ctx.font = `64px ${FONT_DISPLAY}`;
  ctx.lineWidth = 12;
  ctx.strokeStyle = '#1b1033';
  ctx.strokeText(skin.slogan, W / 2, H * 0.36);
  const tg = ctx.createLinearGradient(0, H * 0.2, 0, H * 0.52);
  tg.addColorStop(0, skin.trim);
  tg.addColorStop(1, skin.stripes[0]);
  ctx.fillStyle = tg;
  fitText(ctx, skin.slogan, W / 2, H * 0.36, W - 220, 64, FONT_DISPLAY);
  // route
  ctx.fillStyle = '#1b1033';
  fitText(ctx, skin.route, W / 2, H * 0.6, 520, 28, '900 ' + FONT_BODY, true);
  return canvasTexture(c);
}

/** Lower rear panel: brushed body, tail art, plate. 512x256 */
export function jeepneyRearTexture(skin: JeepneySkin): THREE.CanvasTexture {
  const W = 512, H = 256;
  const c = newCanvas(W, H);
  const ctx = c.getContext('2d')!;
  brushed(ctx, W, H, skin.body);
  skin.stripes.forEach((col, i) => {
    ctx.fillStyle = col;
    ctx.fillRect(0, H * 0.72 + i * 10, W, 7);
  });
  // plate
  ctx.fillStyle = '#ffffff';
  ctx.fillRect(W / 2 - 80, H * 0.36, 160, 50);
  ctx.strokeStyle = '#0a7b3e';
  ctx.lineWidth = 5;
  ctx.strokeRect(W / 2 - 80, H * 0.36, 160, 50);
  ctx.fillStyle = '#0a7b3e';
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  fitText(ctx, skin.plate, W / 2, H * 0.36 + 27, 140, 34, '900 ' + FONT_BODY, true);
  for (const fx of [60, W - 60]) star(ctx, fx, H * 0.4, 26, skin.trim);
  return canvasTexture(c);
}

/** Header board with the famous rear slogan. 512x96 */
export function jeepneyHeaderTexture(text: string, skin: JeepneySkin): THREE.CanvasTexture {
  const W = 512, H = 96;
  const c = newCanvas(W, H);
  const ctx = c.getContext('2d')!;
  ctx.fillStyle = '#1b1033';
  ctx.fillRect(0, 0, W, H);
  const g = ctx.createLinearGradient(0, 0, W, 0);
  skin.stripes.forEach((s, i) => g.addColorStop(i / Math.max(1, skin.stripes.length - 1), s));
  ctx.fillStyle = g;
  ctx.fillRect(0, 0, W, 8);
  ctx.fillRect(0, H - 8, W, 8);
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.fillStyle = skin.trim;
  fitText(ctx, text, W / 2, H / 2 + 3, W - 40, 50, FONT_DISPLAY);
  return canvasTexture(c);
}
