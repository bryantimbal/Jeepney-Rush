/**
 * Renders every texture the Unity project needs (road, facades, signs, jeepney
 * art, UI logo) with HTML canvas in headless Chromium and writes PNGs into
 * unity/JeepneyRush/Assets/JeepneyRush/Resources/Textures.
 *
 *   node scripts/unity-textures.mjs
 *
 * All artwork is original and generated from code.
 */
import { createRequire } from 'node:module';
import { execSync } from 'node:child_process';
import { writeFileSync, mkdirSync } from 'node:fs';
import { join } from 'node:path';

const require = createRequire(import.meta.url);
let pw;
try { pw = require('playwright'); } catch { pw = require(`${execSync('npm root -g').toString().trim()}/playwright`); }

const root = new URL('..', import.meta.url).pathname;
const out = join(root, 'unity/JeepneyRush/Assets/JeepneyRush/Resources/Textures');
mkdirSync(out, { recursive: true });
const font = (f) => 'file://' + join(root, 'node_modules/@fontsource', f);

const browser = await pw.chromium.launch();
const page = await browser.newPage();
await page.goto('file://' + join(root, 'package.json'));
await page.setContent(`<html><head><style>
@font-face{font-family:'Luckiest Guy';src:url(${font('luckiest-guy/files/luckiest-guy-latin-400-normal.woff2')})}
@font-face{font-family:'Nunito';font-weight:900;src:url(${font('nunito/files/nunito-latin-900-normal.woff2')})}
@font-face{font-family:'Nunito';font-weight:800;src:url(${font('nunito/files/nunito-latin-800-normal.woff2')})}
</style></head><body><span style="font-family:'Luckiest Guy'">a</span><span style="font-family:Nunito;font-weight:900">a</span><span style="font-family:Nunito;font-weight:800">a</span></body></html>`);
await page.evaluate(async () => { await document.fonts.load('40px "Luckiest Guy"'); await document.fonts.load('900 20px Nunito'); await document.fonts.load('800 20px Nunito'); await document.fonts.ready; });

const textures = await page.evaluate(() => {
  const D = '"Luckiest Guy", Impact, sans-serif';
  const B = 'Nunito, Arial, sans-serif';
  const out = {};
  const cv = (w, h) => { const c = document.createElement('canvas'); c.width = w; c.height = h; return [c, c.getContext('2d')]; };
  const noise = (ctx, w, h, n, a, light) => { for (let i = 0; i < n; i++) { const v = Math.floor(Math.random() * 60) + (light ? 170 : 20); ctx.fillStyle = `rgba(${v},${v},${v},${a * Math.random()})`; ctx.fillRect(Math.random() * w, Math.random() * h, 1 + Math.random() * 2, 1 + Math.random() * 2); } };
  const fit = (ctx, text, x, y, maxW, size, fontFn) => { let s = size; do { ctx.font = fontFn(s); if (ctx.measureText(text).width <= maxW) break; s -= 2; } while (s > 8); return s; };
  const outlined = (ctx, text, x, y, maxW, size, fill, stroke, lw, fontFn = (s) => `${s}px ${D}`) => {
    fit(ctx, text, x, y, maxW, size, fontFn); ctx.lineJoin = 'round'; ctx.lineWidth = lw; ctx.strokeStyle = stroke; ctx.strokeText(text, x, y); ctx.fillStyle = fill; ctx.fillText(text, x, y);
  };
  const star = (ctx, x, y, r, color) => { ctx.fillStyle = color; ctx.beginPath(); for (let i = 0; i < 10; i++) { const a = (i / 10) * Math.PI * 2 - Math.PI / 2; const rr = i % 2 ? r * 0.45 : r; ctx.lineTo(x + Math.cos(a) * rr, y + Math.sin(a) * rr); } ctx.fill(); };
  const sun = (ctx, x, y, r, color) => {
    ctx.fillStyle = color;
    ctx.beginPath(); ctx.arc(x, y, r, 0, Math.PI * 2); ctx.fill();
    for (let i = 0; i < 8; i++) { const a = (i / 8) * Math.PI * 2; ctx.save(); ctx.translate(x, y); ctx.rotate(a); ctx.beginPath(); ctx.moveTo(r * 1.15, -r * 0.18); ctx.lineTo(r * 2.1, 0); ctx.lineTo(r * 1.15, r * 0.18); ctx.fill(); ctx.beginPath(); ctx.moveTo(r * 1.15, -r * 0.42); ctx.lineTo(r * 1.75, -r * 0.3); ctx.lineTo(r * 1.15, -r * 0.2); ctx.fill(); ctx.beginPath(); ctx.moveTo(r * 1.15, r * 0.42); ctx.lineTo(r * 1.75, r * 0.3); ctx.lineTo(r * 1.15, r * 0.2); ctx.fill(); ctx.restore(); }
  };
  const flower = (ctx, x, y, r) => { ctx.fillStyle = '#fff'; for (let i = 0; i < 5; i++) { const a = i / 5 * Math.PI * 2; ctx.beginPath(); ctx.ellipse(x + Math.cos(a) * r * 0.6, y + Math.sin(a) * r * 0.6, r * 0.55, r * 0.3, a, 0, Math.PI * 2); ctx.fill(); } ctx.fillStyle = '#ffd23f'; ctx.beginPath(); ctx.arc(x, y, r * 0.3, 0, Math.PI * 2); ctx.fill(); };
  const gloss = (ctx, w, h) => { const g = ctx.createLinearGradient(0, 0, 0, h); g.addColorStop(0, 'rgba(255,255,255,0.22)'); g.addColorStop(0.35, 'rgba(255,255,255,0)'); g.addColorStop(1, 'rgba(0,0,0,0.18)'); ctx.fillStyle = g; ctx.fillRect(0, 0, w, h); };
  const save = (name, c) => (out[name] = c.toDataURL('image/png'));

  // -------------------------------------------------- jeepney: Pilipinas Classic
  {
    const [c, x] = cv(1024, 256);
    x.fillStyle = '#C8102E'; x.fillRect(0, 0, 1024, 256);
    // blue + white pinstripes at the top edge
    x.fillStyle = '#1F3FBF'; x.fillRect(0, 0, 1024, 18); x.fillStyle = '#fff'; x.fillRect(0, 18, 1024, 5);
    // sun with rays + three stars (flag motif)
    sun(x, 200, 128, 34, '#FCD116');
    star(x, 330, 70, 20, '#FCD116'); star(x, 380, 52, 20, '#FCD116'); star(x, 430, 70, 20, '#FCD116');
    // yellow sweep
    x.fillStyle = '#FCD116'; x.beginPath(); x.moveTo(0, 256); x.bezierCurveTo(300, 150, 600, 250, 1024, 175); x.lineTo(1024, 200); x.bezierCurveTo(600, 270, 300, 175, 0, 256); x.fill();
    x.textAlign = 'center'; x.textBaseline = 'middle';
    outlined(x, 'Pilipinas', 640, 120, 520, 120, '#ffffff', '#0B1E6B', 16);
    x.fillStyle = '#fff'; fit(x, 'LUZON  •  VISAYAS  •  MINDANAO', 640, 42, 520, 22, (s) => `800 ${s}px ${B}`); x.fillText('LUZON  •  VISAYAS  •  MINDANAO', 640, 42);
    for (let i = 0; i < 4; i++) flower(x, 880 + (i % 2) * 50, 60 + i * 30, 14);
    gloss(x, 1024, 256);
    save('jeepney_side_pilipinas', c);
  }
  {
    const [c, x] = cv(512, 416);
    x.fillStyle = '#C8102E'; x.fillRect(0, 0, 512, 416);
    x.fillStyle = '#1B1033'; x.fillRect(0, 0, 512, 70);
    x.textAlign = 'center'; x.textBaseline = 'middle';
    x.fillStyle = '#FCD116'; fit(x, 'GOD BLESS OUR TRIP', 256, 38, 470, 44, (s) => `${s}px ${D}`); x.fillText('GOD BLESS OUR TRIP', 256, 38);
    // open doorway with benches inside
    x.fillStyle = '#140F1A'; x.fillRect(150, 80, 212, 336);
    x.fillStyle = '#8B1E2D'; x.fillRect(150, 250, 40, 166); x.fillRect(322, 250, 40, 166);
    x.fillStyle = '#ffe9a8'; x.fillRect(200, 84, 112, 6);
    sun(x, 75, 170, 26, '#FCD116'); sun(x, 437, 170, 26, '#FCD116');
    // plate
    x.fillStyle = '#fff'; x.fillRect(20, 300, 120, 44); x.strokeStyle = '#0A7B3E'; x.lineWidth = 4; x.strokeRect(20, 300, 120, 44);
    x.fillStyle = '#0A7B3E'; fit(x, 'PUJ 2026', 80, 323, 108, 28, (s) => `900 ${s}px ${B}`); x.fillText('PUJ 2026', 80, 323);
    x.fillStyle = '#FCD116'; x.fillRect(0, 380, 150, 10); x.fillRect(362, 380, 150, 10);
    gloss(x, 512, 416);
    save('jeepney_rear_pilipinas', c);
  }
  {
    const [c, x] = cv(512, 128);
    x.fillStyle = '#fff'; x.fillRect(0, 0, 512, 128);
    x.fillStyle = '#1F3FBF'; x.fillRect(0, 0, 512, 10); x.fillStyle = '#C8102E'; x.fillRect(0, 118, 512, 10);
    x.textAlign = 'center'; x.textBaseline = 'middle';
    outlined(x, 'PILIPINAS', 256, 68, 470, 84, '#1F3FBF', '#C8102E', 8);
    save('jeepney_sign_pilipinas', c);
  }
  // -------------------------------------------------- jeepney: San Juan Express
  {
    const [c, x] = cv(1024, 256);
    x.fillStyle = '#8E2BD9'; x.fillRect(0, 0, 1024, 256);
    // sunburst from the front-lower corner (front is u=0 on the art)
    const cols = ['#FF8A1F', '#FFD23F', '#FFFFFF', '#FF5A36', '#FFB703'];
    for (let i = 0; i < 9; i++) { x.fillStyle = cols[i % cols.length]; x.beginPath(); x.moveTo(-40, 290); const a0 = -Math.PI / 2 + i * 0.13, a1 = a0 + 0.07; x.lineTo(-40 + Math.cos(a0) * 700, 290 + Math.sin(a0) * 700); x.lineTo(-40 + Math.cos(a1) * 700, 290 + Math.sin(a1) * 700); x.fill(); }
    x.fillStyle = '#8E2BD9'; x.fillRect(420, 0, 604, 256);
    x.fillStyle = '#25C98B'; x.fillRect(0, 196, 1024, 14); x.fillStyle = '#FFD23F'; x.fillRect(0, 214, 1024, 10); x.fillStyle = '#D62828'; x.fillRect(0, 228, 1024, 10);
    x.textAlign = 'center'; x.textBaseline = 'middle';
    outlined(x, 'San Juan - San Fernando', 700, 100, 560, 70, '#ffffff', '#3C0F66', 10);
    star(x, 470, 60, 14, '#FFD23F'); star(x, 940, 60, 14, '#FFD23F');
    gloss(x, 1024, 256);
    save('jeepney_side_sanjuan', c);
  }
  {
    const [c, x] = cv(512, 416);
    x.fillStyle = '#8E2BD9'; x.fillRect(0, 0, 512, 416);
    x.fillStyle = '#3C0F66'; x.fillRect(0, 0, 512, 70);
    x.textAlign = 'center'; x.textBaseline = 'middle';
    x.fillStyle = '#FFD23F'; fit(x, 'SALAMAT PO!', 256, 38, 470, 46, (s) => `${s}px ${D}`); x.fillText('SALAMAT PO!', 256, 38);
    x.fillStyle = '#140F1A'; x.fillRect(150, 80, 212, 336);
    x.fillStyle = '#8B1E2D'; x.fillRect(150, 250, 40, 166); x.fillRect(322, 250, 40, 166);
    x.fillStyle = '#25C98B'; x.fillRect(0, 360, 150, 12); x.fillRect(362, 360, 150, 12);
    x.fillStyle = '#FF8A1F'; x.fillRect(0, 376, 150, 10); x.fillRect(362, 376, 150, 10);
    x.fillStyle = '#fff'; x.fillRect(20, 300, 120, 44); x.strokeStyle = '#0A7B3E'; x.lineWidth = 4; x.strokeRect(20, 300, 120, 44);
    x.fillStyle = '#0A7B3E'; fit(x, 'SJ 1965', 80, 323, 108, 28, (s) => `900 ${s}px ${B}`); x.fillText('SJ 1965', 80, 323);
    gloss(x, 512, 416);
    save('jeepney_rear_sanjuan', c);
  }
  {
    const [c, x] = cv(512, 128);
    x.fillStyle = '#8E2BD9'; x.fillRect(0, 0, 512, 128);
    x.fillStyle = '#FFD23F'; x.fillRect(0, 96, 512, 10); x.fillStyle = '#25C98B'; x.fillRect(0, 106, 512, 8);
    x.textAlign = 'center'; x.textBaseline = 'middle';
    x.fillStyle = '#fff'; fit(x, 'SAN JUAN - SAN FERNANDO', 256, 52, 480, 52, (s) => `${s}px ${D}`); x.fillText('SAN JUAN - SAN FERNANDO', 256, 52);
    save('jeepney_sign_sanjuan', c);
  }

  // -------------------------------------------------- road: 3 lanes x 3.2 m + shoulders = 11.2 m wide, 12 m per tile
  {
    const W = 512, H = 512, roadW = 11.2;
    const [c, x] = cv(W, H);
    x.fillStyle = '#45474e'; x.fillRect(0, 0, W, H);
    noise(x, W, H, 9000, 0.35); noise(x, W, H, 2500, 0.15, true);
    for (let i = 0; i < 4; i++) { x.fillStyle = `rgba(20,20,24,${0.1 + Math.random() * 0.1})`; x.beginPath(); x.ellipse(Math.random() * W, Math.random() * H, 20 + Math.random() * 40, 30 + Math.random() * 60, Math.random(), 0, Math.PI * 2); x.fill(); }
    const px = (m) => W / 2 + (m / roadW) * W;
    for (const lx of [-3.2, 0, 3.2]) { x.fillStyle = 'rgba(25,25,30,0.2)'; x.fillRect(px(lx - 0.8) - 10, 0, 20, H); x.fillRect(px(lx + 0.8) - 10, 0, 20, H); }
    x.fillStyle = '#f2f2ee';
    for (const lx of [-1.6, 1.6]) for (let d = 0; d < 2; d++) x.fillRect(px(lx) - 5, d * H / 2 + H / 8, 10, H / 4);
    x.fillStyle = '#f5c518'; x.fillRect(px(-5.2) - 6, 0, 12, H); x.fillRect(px(5.2) - 6, 0, 12, H);
    save('road', c);
  }
  {
    const [c, x] = cv(256, 256);
    x.fillStyle = '#bdb6aa'; x.fillRect(0, 0, 256, 256); noise(x, 256, 256, 3000, 0.25);
    x.strokeStyle = 'rgba(80,70,60,0.45)'; x.lineWidth = 3;
    for (let i = 0; i <= 2; i++) { x.beginPath(); x.moveTo(0, i * 128); x.lineTo(256, i * 128); x.moveTo(i * 128, 0); x.lineTo(i * 128, 256); x.stroke(); }
    save('sidewalk', c);
  }
  {
    const [c, x] = cv(256, 32);
    for (let i = 0; i < 4; i++) { x.fillStyle = i % 2 ? '#1d1d1f' : '#f2c21b'; x.fillRect(i * 64, 0, 64, 32); }
    noise(x, 256, 32, 300, 0.3);
    save('curb', c);
  }
  // -------------------------------------------------- facade (white, tinted per building), 2x2 window variants
  {
    const S = 256; const [c, x] = cv(S * 2, S * 2);
    x.fillStyle = '#fff'; x.fillRect(0, 0, S * 2, S * 2); noise(x, S * 2, S * 2, 4000, 0.12);
    for (let gx = 0; gx < 2; gx++) for (let gy = 0; gy < 2; gy++) {
      const ox = gx * S, oy = gy * S, v = gx + gy * 2;
      x.fillStyle = '#d9d9d9'; x.fillRect(ox + 40, oy + 50, S - 80, S - 90);
      if (v === 3) { x.fillStyle = '#8f949a'; x.fillRect(ox + 48, oy + 58, S - 96, S - 106); x.fillStyle = 'rgba(0,0,0,0.25)'; for (let y = oy + 60; y < oy + S - 50; y += 8) x.fillRect(ox + 48, y, S - 96, 2); }
      else {
        const g = x.createLinearGradient(0, oy + 58, 0, oy + S - 48); g.addColorStop(0, '#2b3d55'); g.addColorStop(1, '#5a7898'); x.fillStyle = g; x.fillRect(ox + 48, oy + 58, S - 96, S - 106);
        x.fillStyle = 'rgba(255,255,255,0.25)'; x.fillRect(ox + S / 2 - 3, oy + 58, 6, S - 106); x.fillRect(ox + 48, oy + 110, S - 96, 5);
        x.strokeStyle = 'rgba(30,30,30,0.55)'; x.lineWidth = 3; for (let xx = ox + 60; xx < ox + S - 48; xx += 18) { x.beginPath(); x.moveTo(xx, oy + 58); x.lineTo(xx, oy + S - 48); x.stroke(); }
      }
      if (v === 1) { x.fillStyle = '#e8e8e8'; x.fillRect(ox + 150, oy + 170, 60, 40); x.fillStyle = '#9a9a9a'; for (let y = oy + 176; y < oy + 206; y += 6) x.fillRect(ox + 156, y, 48, 2); }
      if (v === 2) { x.strokeStyle = '#444'; x.lineWidth = 2; x.beginPath(); x.moveTo(ox + 40, oy + 215); x.lineTo(ox + S - 40, oy + 215); x.stroke(); ['#e63946', '#2a9d8f', '#f4a261', '#457b9d', '#ffd166'].forEach((col, i) => { x.fillStyle = col; x.fillRect(ox + 55 + i * 30, oy + 215, 20, 26); }); }
      x.fillStyle = 'rgba(0,0,0,0.18)'; x.fillRect(ox, oy + S - 14, S, 14);
    }
    save('facade', c);
  }
  // -------------------------------------------------- shop sign atlas (4 x 4 cells of 256 x 96)
  {
    const signs = [
      ['SARI-SARI STORE', 'Ice • Load • Softdrinks', '#e63946', '#fff', '#ffd166'], ['CARINDERIA', 'Lutong Bahay', '#ffd166', '#7a1f1f', '#e63946'],
      ['LOAD NA DITO!', 'E-Load • Pasaload', '#06d6a0', '#073b4c', '#fff'], ['PANADERIA', 'Mainit na Pandesal', '#f4a261', '#3d1f00', '#fff3d6'],
      ['VULCANIZING', '24 Hours', '#1d3557', '#ffd166', '#e63946'], ['BOTIKA', 'Generics • Vitamins', '#2a9d8f', '#fff', '#e9f5db'],
      ['LECHON MANOK', 'Inasal • Liempo', '#9d0208', '#ffba08', '#ffba08'], ['HALO-HALO', 'Mais con Yelo', '#9b5de5', '#fff', '#fee440'],
      ['BARBERSHOP', 'Gupit P80', '#f1faee', '#1d3557', '#e63946'], ['KAIN NA!', 'Silog Meals P99', '#ff7b00', '#fff', '#ffd000'],
      ['PANSITAN', 'Canton • Bihon', '#ffbe0b', '#3a0ca3', '#fb5607'], ['TERMINAL', 'Jeepney Terminal', '#264653', '#e9c46a', '#e76f51'],
      ['FIESTA SALE!', 'Lahat Mura!', '#ef476f', '#fff', '#ffd166'], ['TAHO', 'Arnibal • Sago', '#bc6c25', '#fefae0', '#dda15e'],
      ['MERIENDA', 'Turon • Banana Cue', '#ffd60a', '#003566', '#ffc300'], ['INTERNET CAFE', 'P15 / hour', '#3a86ff', '#fff', '#8338ec'],
    ];
    const CW = 256, CH = 96; const [c, x] = cv(CW * 4, CH * 4);
    signs.forEach(([t, s, bg, fg, ac], i) => {
      const ox = (i % 4) * CW, oy = Math.floor(i / 4) * CH;
      x.fillStyle = bg; x.fillRect(ox, oy, CW, CH); x.strokeStyle = ac; x.lineWidth = 6; x.strokeRect(ox + 5, oy + 5, CW - 10, CH - 10);
      x.textAlign = 'center'; x.textBaseline = 'middle';
      x.fillStyle = fg; fit(x, t, 0, 0, CW - 30, 40, (z) => `${z}px ${D}`); x.fillText(t, ox + CW / 2, oy + CH * 0.42);
      x.fillStyle = ac; fit(x, s, 0, 0, CW - 40, 17, (z) => `800 ${z}px ${B}`); x.fillText(s, ox + CW / 2, oy + CH * 0.78);
    });
    save('shop_signs', c);
  }
  // -------------------------------------------------- stop name signs (4 x 8 cells of 256 x 128)
  {
    const names = ['QUIAPO', 'CUBAO', 'DIVISORIA', 'RECTO', 'LAWTON', 'ESPANA', 'TAFT', 'MONUMENTO', 'BACLARAN', 'BLUMENTRITT', 'KALAW', 'PACO', 'STA. MESA', 'PEDRO GIL', 'VITO CRUZ', 'LIBERTAD', 'SAMPALOC', 'ERMITA', 'MALATE', 'TONDO'];
    const CW = 256, CH = 128; const [c, x] = cv(CW * 4, CH * 8);
    names.forEach((n, i) => {
      const ox = (i % 4) * CW, oy = Math.floor(i / 4) * CH;
      x.fillStyle = '#0b6e4f'; x.fillRect(ox, oy, CW, CH); x.strokeStyle = '#fff'; x.lineWidth = 6; x.strokeRect(ox + 6, oy + 6, CW - 12, CH - 12);
      x.textAlign = 'center'; x.textBaseline = 'middle';
      x.fillStyle = '#ffd100'; fit(x, 'SAKAYAN', 0, 0, CW - 30, 24, (z) => `900 ${z}px ${B}`); x.fillText('SAKAYAN', ox + CW / 2, oy + CH * 0.27);
      x.fillStyle = '#fff'; fit(x, n, 0, 0, CW - 30, 52, (z) => `${z}px ${D}`); x.fillText(n, ox + CW / 2, oy + CH * 0.65);
    });
    save('stop_signs', c);
  }
  // -------------------------------------------------- PARA zone road decal
  {
    const [c, x] = cv(128, 512);
    x.fillStyle = 'rgba(255,209,0,0.30)'; x.fillRect(0, 0, 128, 512);
    x.strokeStyle = '#ffd100'; x.lineWidth = 10; x.strokeRect(5, 5, 118, 502);
    x.strokeStyle = 'rgba(255,209,0,0.6)'; x.lineWidth = 6; for (let y = -128; y < 512; y += 40) { x.beginPath(); x.moveTo(0, y); x.lineTo(128, y + 128); x.stroke(); }
    x.save(); x.translate(64, 256); x.rotate(-Math.PI / 2); x.textAlign = 'center'; x.textBaseline = 'middle';
    outlined(x, 'PARA', 0, 4, 400, 64, '#fff', '#7a4b00', 6); x.restore();
    save('para_zone', c);
  }
  // -------------------------------------------------- UI logo
  {
    const [c, x] = cv(1024, 520);
    x.textAlign = 'center'; x.textBaseline = 'middle'; x.lineJoin = 'round';
    x.font = `190px ${D}`;
    x.lineWidth = 34; x.strokeStyle = '#1b1033'; x.strokeText('JEEPNEY', 512, 160);
    x.fillStyle = '#ce1126'; x.fillText('JEEPNEY', 512, 172);
    x.lineWidth = 16; x.strokeStyle = '#1b1033'; x.strokeText('JEEPNEY', 512, 160);
    x.fillStyle = '#fcd116'; x.fillText('JEEPNEY', 512, 160);
    x.save(); x.translate(512, 360); x.rotate(-0.05); x.font = `250px ${D}`;
    x.lineWidth = 38; x.strokeStyle = '#1b1033'; x.strokeText('RUSH', 0, 0);
    x.fillStyle = '#0038a8'; x.fillText('RUSH', 0, 14);
    x.lineWidth = 18; x.strokeStyle = '#1b1033'; x.strokeText('RUSH', 0, 0);
    x.fillStyle = '#ffffff'; x.fillText('RUSH', 0, 0); x.restore();
    save('logo', c);
  }
  return out;
});

for (const [name, url] of Object.entries(textures)) {
  writeFileSync(join(out, name + '.png'), Buffer.from(url.split(',')[1], 'base64'));
  console.log('wrote', name + '.png');
}
await browser.close();
