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
    noise(x, W, H, 3500, 0.18); noise(x, W, H, 900, 0.08, true);
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
    x.fillStyle = '#bdb6aa'; x.fillRect(0, 0, 256, 256); noise(x, 256, 256, 1200, 0.12);
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
  // -------------------------------------------------- stop name signs (4 x 14 cells of 256 x 128)
  {
    // must match StopNames.All in Environments.cs (Manila 0-19, Provincial 20-31, Beach 32-43, Mountain 44-55)
    const names = ['QUIAPO', 'CUBAO', 'DIVISORIA', 'RECTO', 'LAWTON', 'ESPANA', 'TAFT', 'MONUMENTO', 'BACLARAN', 'BLUMENTRITT', 'KALAW', 'PACO', 'STA. MESA', 'PEDRO GIL', 'VITO CRUZ', 'LIBERTAD', 'SAMPALOC', 'ERMITA', 'MALATE', 'TONDO',
      'POBLACION', 'SAN ISIDRO', 'PALENGKE', 'SIMBAHAN', 'MUNISIPYO', 'BRGY. MALIGAYA', 'BAYAN', 'PLAZA', 'CROSSING', 'SAN ROQUE', 'KAPITOLYO', 'TERMINAL',
      'WHITE BEACH', 'PUERTO', 'BAYBAYIN', 'PIER', 'LIGHTHOUSE', 'RESORT', 'DAUNGAN', 'FISHPORT', 'BOULEVARD', 'COVE', 'LAGUNDI', 'DIVE SHOP',
      'BONTOC', 'SAGADA', 'BESAO', 'SABANGAN', 'TADIAN', 'BAUKO', 'SADANGA', 'BARLIG', 'NATONIN', 'ECHO VALLEY', 'RICE TERRACES', 'VIEWDECK'];
    const CW = 256, CH = 128; const [c, x] = cv(CW * 4, CH * 14);
    names.forEach((n, i) => {
      const ox = (i % 4) * CW, oy = Math.floor(i / 4) * CH;
      x.fillStyle = i < 20 ? '#0b6e4f' : i < 32 ? '#7a4e2d' : i < 44 ? '#0077b6' : '#3f3326'; x.fillRect(ox, oy, CW, CH); x.strokeStyle = '#fff'; x.lineWidth = 6; x.strokeRect(ox + 6, oy + 6, CW - 12, CH - 12);
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
  // -------------------------------------------------- UI icons & backgrounds
  {
    const icon = (name, w, h, draw) => { const [c, x] = cv(w, h); draw(x, w, h); save(name, c); };
    const heart = (x, w, fill) => { x.save(); x.translate(w / 2, w / 2 + 6); x.scale(w / 128, w / 128); x.beginPath(); x.moveTo(0, 38); x.bezierCurveTo(-70, -10, -40, -70, 0, -32); x.bezierCurveTo(40, -70, 70, -10, 0, 38); x.closePath(); x.lineWidth = 12; x.strokeStyle = '#1b1033'; x.lineJoin = 'round'; x.stroke(); x.fillStyle = fill; x.fill(); x.fillStyle = 'rgba(255,255,255,0.45)'; x.beginPath(); x.ellipse(-22, -24, 12, 8, -0.6, 0, Math.PI * 2); x.fill(); x.restore(); };
    icon('ui_heart', 128, 128, (x, w) => heart(x, w, '#ff3b4f'));
    icon('ui_heart_empty', 128, 128, (x, w) => heart(x, w, '#5b5470'));
    icon('ui_coin', 128, 128, (x) => { const g = x.createRadialGradient(48, 44, 6, 64, 64, 60); g.addColorStop(0, '#fff6b0'); g.addColorStop(0.45, '#ffc928'); g.addColorStop(1, '#c98a00'); x.fillStyle = g; x.beginPath(); x.arc(64, 64, 56, 0, Math.PI * 2); x.fill(); x.lineWidth = 8; x.strokeStyle = '#8a5a00'; x.stroke(); x.fillStyle = '#8a5a00'; x.textAlign = 'center'; x.textBaseline = 'middle'; x.font = `64px ${D}`; x.fillText('1', 64, 70); });
    icon('ui_person', 128, 128, (x) => { x.fillStyle = '#ffd23f'; x.strokeStyle = '#1b1033'; x.lineWidth = 8; x.beginPath(); x.arc(64, 36, 22, 0, Math.PI * 2); x.fill(); x.stroke(); x.beginPath(); x.roundRect(30, 64, 68, 56, 22); x.fill(); x.stroke(); });
    icon('ui_pause', 128, 128, (x) => { x.fillStyle = '#1b1033'; x.beginPath(); x.roundRect(34, 26, 22, 76, 6); x.roundRect(72, 26, 22, 76, 6); x.fill(); });
    icon('ui_horn', 128, 128, (x) => { x.fillStyle = '#ffd23f'; x.strokeStyle = '#1b1033'; x.lineWidth = 8; x.lineJoin = 'round'; x.beginPath(); x.moveTo(18, 52); x.lineTo(46, 52); x.lineTo(100, 22); x.lineTo(100, 106); x.lineTo(46, 76); x.lineTo(18, 76); x.closePath(); x.fill(); x.stroke(); x.strokeStyle = '#1b1033'; x.lineWidth = 7; x.beginPath(); x.arc(100, 64, 18, -0.9, 0.9); x.stroke(); });
    const arrow = (x, dir) => { x.save(); x.translate(64, 64); x.scale(dir, 1); x.fillStyle = '#ffd23f'; x.strokeStyle = '#1b1033'; x.lineWidth = 10; x.lineJoin = 'round'; x.beginPath(); x.moveTo(-40, 0); x.lineTo(10, -42); x.lineTo(10, -16); x.lineTo(42, -16); x.lineTo(42, 16); x.lineTo(10, 16); x.lineTo(10, 42); x.closePath(); x.stroke(); x.fill(); x.restore(); };
    icon('ui_arrow_left', 128, 128, (x) => arrow(x, 1));
    icon('ui_arrow_right', 128, 128, (x) => arrow(x, -1));
    icon('ui_splash_bg', 512, 1024, (x, w, h) => {
      const g = x.createRadialGradient(w / 2, h * 0.35, 20, w / 2, h * 0.4, h * 0.8); g.addColorStop(0, '#ffb347'); g.addColorStop(0.35, '#ff6a3d'); g.addColorStop(0.75, '#b5179e'); g.addColorStop(1, '#3a0ca3'); x.fillStyle = g; x.fillRect(0, 0, w, h);
      x.save(); x.translate(w / 2, h * 0.35); for (let i = 0; i < 18; i++) { x.rotate(Math.PI / 9); x.fillStyle = 'rgba(255,255,255,0.09)'; x.beginPath(); x.moveTo(0, 0); x.lineTo(-90, -1200); x.lineTo(90, -1200); x.fill(); } x.restore();
    });
    icon('ui_jeepney', 440, 220, (x) => {
      x.scale(2, 2);
      x.fillStyle = 'rgba(0,0,0,0.35)'; x.fillRect(8, 88, 200, 6);
      x.fillStyle = '#d7dbe2'; x.fillRect(60, 28, 120, 54);
      x.fillStyle = '#ce1126'; x.fillRect(60, 62, 120, 6); x.fillStyle = '#fcd116'; x.fillRect(60, 69, 120, 4); x.fillStyle = '#0038a8'; x.fillRect(60, 74, 120, 4);
      x.fillStyle = '#0038a8'; x.fillRect(56, 20, 132, 9); x.fillStyle = '#1d2a3a'; x.fillRect(66, 33, 108, 18);
      x.fillStyle = '#fcd116'; for (const px of [80, 100, 120, 140, 160]) x.fillRect(px, 33, 4, 18);
      x.fillStyle = '#ce1126'; x.fillRect(22, 50, 40, 22); x.fillStyle = '#e9edf2'; x.fillRect(14, 56, 10, 20); x.fillRect(8, 74, 20, 6);
      x.fillStyle = '#fff7d6'; x.beginPath(); x.arc(20, 60, 4, 0, Math.PI * 2); x.fill();
      x.fillStyle = '#1b1033'; x.font = `9px ${D}`; x.textAlign = 'center'; x.fillText('PILIPINAS', 120, 61);
      x.fillStyle = '#ff2a2a'; x.fillRect(176, 40, 5, 16);
      for (const cx of [45, 160]) { x.fillStyle = '#17171a'; x.beginPath(); x.arc(cx, 84, 11, 0, Math.PI * 2); x.fill(); x.fillStyle = '#e9edf2'; x.beginPath(); x.arc(cx, 84, 5, 0, Math.PI * 2); x.fill(); }
    });
  }
  // -------------------------------------------------- Phase 2 jeepney liveries (match JeepneyCatalog.cs)
  {
    const L = [
      { id: 'manila', body: '#D7DBE2', a1: '#C1121F', a2: '#1D3557', motif: 'manila', slogan: 'Hari ng Kalsada', route: 'DIVISORIA - BACLARAN', rear: 'BAWAL ANG SIMANGOT', plate: 'MNL 1953', sign: 'DIVISORIA', signBg: '#FFFFFF', signFg: '#C1121F' },
      { id: 'modern', body: '#F8F9FA', a1: '#00A6A6', a2: '#F4A261', motif: 'waves', slogan: 'Modern PUV', route: 'BGC - AYALA', rear: 'PLEASE PAY EXACT FARE', plate: 'MPUJ 01', sign: 'BGC - AYALA', signBg: '#0A0A0A', signFg: '#FFB000', led: true },
      { id: 'cebu', body: '#0077B6', a1: '#F77F00', a2: '#FFFFFF', motif: 'waves', slogan: 'Sugbo', route: 'COLON - IT PARK', rear: 'PIT SENYOR!', plate: 'CEB 2024', sign: 'CEBU CITY', signBg: '#FCBF49', signFg: '#0077B6' },
      { id: 'baguio', body: '#2D6A4F', a1: '#95D5B2', a2: '#FFFFFF', motif: 'mountains', slogan: 'City of Pines', route: 'SESSION RD - MINES VIEW', rear: 'INGAT SA PAGMAMANEHO', plate: 'BAG 1909', sign: 'BAGUIO', signBg: '#D8F3DC', signFg: '#1B4332' },
      { id: 'davao', body: '#FFB703', a1: '#6B4226', a2: '#FFFFFF', motif: 'eagle', slogan: 'Davao Explorer', route: 'BANGKAL - ROXAS', rear: 'LIFE IS HERE', plate: 'DVO 1937', sign: 'DAVAO', signBg: '#6B4226', signFg: '#FFB703' },
      { id: 'fiesta', body: '#EF476F', a1: '#FFD166', a2: '#118AB2', motif: 'fiesta', slogan: 'Fiesta!', route: 'PISTA SA NAYON', rear: 'MABUHAY!', plate: 'PISTA 7', sign: 'FIESTA!', signBg: '#FFD166', signFg: '#EF476F' },
      { id: 'neon', body: '#14112B', a1: '#00F5D4', a2: '#F15BB5', motif: 'neon', slogan: 'Neon Rush', route: 'MIDNIGHT RUN', rear: 'CATCH ME IF YOU CAN', plate: 'NEON 88', sign: 'NEON', signBg: '#14112B', signFg: '#00F5D4' },
      { id: 'flag', body: '#FFFFFF', a1: '#CE1126', a2: '#FCD116', motif: 'flag', slogan: 'Mabuhay!', route: 'LUZON - VISAYAS - MINDANAO', rear: 'PILIPINAS KONG MAHAL', plate: 'PH 1898', sign: 'MABUHAY', signBg: '#0038A8', signFg: '#FFFFFF' },
      { id: 'gold', body: '#1A1A1A', a1: '#FFD700', a2: '#FFF3B0', motif: 'gold', slogan: 'VIP', route: 'VIP EXPRESS', rear: 'VERY IMPORTANT PASAHERO', plate: 'VIP 1', sign: 'VIP', signBg: '#1A1A1A', signFg: '#FFD700' },
    ];
    const goldGrad = (x, y0, y1) => { const g = x.createLinearGradient(0, y0, 0, y1); g.addColorStop(0, '#FFF3B0'); g.addColorStop(0.5, '#FFD700'); g.addColorStop(1, '#B8860B'); return g; };
    for (const j of L) {
      // ---- side art 1024x256
      let [c, x] = cv(1024, 256);
      x.fillStyle = j.body; x.fillRect(0, 0, 1024, 256);
      x.textAlign = 'center'; x.textBaseline = 'middle';
      switch (j.motif) {
        case 'manila':
          x.strokeStyle = j.a1; x.lineWidth = 6;
          for (const cx of [140, 884]) for (let k = 0; k < 3; k++) { x.beginPath(); x.arc(cx, 128, 30 + k * 22, 0.3 + k, 4.5 + k); x.stroke(); }
          x.fillStyle = j.a2; x.fillRect(0, 205, 1024, 12); x.fillStyle = j.a1; x.fillRect(0, 222, 1024, 8);
          break;
        case 'waves':
          for (let k = 0; k < 4; k++) { x.fillStyle = k % 2 ? j.a2 : j.a1; x.beginPath(); x.moveTo(0, 256); for (let px = 0; px <= 1024; px += 16) x.lineTo(px, 170 + k * 18 + Math.sin(px / 60 + k) * 10); x.lineTo(1024, 256); x.fill(); }
          break;
        case 'mountains':
          for (let k = 0; k < 7; k++) { x.fillStyle = k % 2 ? '#1B4332' : j.a1; x.beginPath(); const mx = k * 160 - 40; x.moveTo(mx, 230); x.lineTo(mx + 110, 120 + (k % 3) * 15); x.lineTo(mx + 220, 230); x.fill(); }
          x.fillStyle = '#FFFFFF'; for (let k = 0; k < 7; k++) { const mx = k * 160 - 40 + 110; x.beginPath(); x.moveTo(mx - 18, 150 + (k % 3) * 15); x.lineTo(mx, 120 + (k % 3) * 15); x.lineTo(mx + 18, 150 + (k % 3) * 15); x.fill(); }
          x.fillStyle = j.a2; x.fillRect(0, 230, 1024, 10);
          break;
        case 'eagle':
          for (const cx of [170, 854]) { x.save(); x.translate(cx, 110); x.fillStyle = j.a1; x.beginPath(); x.moveTo(-110, -10); x.quadraticCurveTo(-40, -60, 0, 0); x.quadraticCurveTo(40, -60, 110, -10); x.quadraticCurveTo(40, 10, 0, 40); x.quadraticCurveTo(-40, 10, -110, -10); x.fill(); x.fillStyle = '#FFFFFF'; x.beginPath(); x.arc(0, -8, 16, 0, Math.PI * 2); x.fill(); x.fillStyle = '#FFB703'; x.beginPath(); x.moveTo(10, -10); x.lineTo(28, -2); x.lineTo(10, 2); x.fill(); x.restore(); }
          x.fillStyle = j.a1; x.fillRect(0, 210, 1024, 16);
          break;
        case 'fiesta':
          for (let k = 0; k < 220; k++) { x.fillStyle = ['#FFD166', '#06D6A0', '#118AB2', '#FFFFFF', '#F78C6B'][k % 5]; x.beginPath(); x.arc(Math.random() * 1024, Math.random() * 256, 3 + Math.random() * 5, 0, Math.PI * 2); x.fill(); }
          for (let k = 0; k < 26; k++) { x.fillStyle = ['#FFD166', '#06D6A0', '#118AB2', '#FFFFFF'][k % 4]; x.beginPath(); x.moveTo(k * 40, 0); x.lineTo(k * 40 + 40, 0); x.lineTo(k * 40 + 20, 36); x.fill(); }
          break;
        case 'neon':
          x.strokeStyle = 'rgba(0,245,212,0.25)'; x.lineWidth = 2; for (let px = 0; px < 1024; px += 32) { x.beginPath(); x.moveTo(px, 140); x.lineTo(px * 1.3 - 150, 256); x.stroke(); }
          x.shadowBlur = 18; for (const [col, y] of [[j.a1, 190], [j.a2, 212]]) { x.shadowColor = col; x.strokeStyle = col; x.lineWidth = 6; x.beginPath(); x.moveTo(0, y); x.lineTo(1024, y - 40); x.stroke(); }
          x.shadowBlur = 0;
          break;
        case 'flag': {
          x.fillStyle = '#0038A8'; x.fillRect(0, 0, 1024, 128); x.fillStyle = '#CE1126'; x.fillRect(0, 128, 1024, 128);
          x.fillStyle = '#FFFFFF'; x.beginPath(); x.moveTo(0, 0); x.lineTo(300, 128); x.lineTo(0, 256); x.fill();
          sun(x, 90, 128, 30, '#FCD116'); star(x, 22, 26, 14, '#FCD116'); star(x, 22, 230, 14, '#FCD116'); star(x, 250, 128, 14, '#FCD116');
          break;
        }
        case 'gold':
          x.strokeStyle = goldGrad(x, 0, 256); x.lineWidth = 4;
          for (let k = 0; k < 6; k++) { x.beginPath(); for (let px = 0; px <= 1024; px += 12) x.lineTo(px, 30 + k * 40 + Math.sin(px / 40 + k) * 8); x.stroke(); }
          x.fillStyle = 'rgba(26,26,26,0.75)'; x.fillRect(250, 50, 524, 140);
          break;
      }
      const fill = j.motif === 'gold' ? goldGrad(x, 60, 170) : j.motif === 'neon' ? j.a1 : '#FFFFFF';
      const stroke = j.motif === 'flag' || j.motif === 'gold' || j.motif === 'neon' ? '#000000' : j.a1 === '#FFFFFF' ? '#1B1033' : Palette(j.a1);
      if (j.motif === 'neon') { x.shadowColor = j.a1; x.shadowBlur = 20; }
      outlined(x, j.slogan, 600, 110, 560, 110, fill, stroke, 12);
      x.shadowBlur = 0;
      x.fillStyle = j.motif === 'neon' ? j.a2 : '#FFFFFF'; fit(x, j.route, 0, 0, 520, 24, (z) => `900 ${z}px ${B}`); x.fillText(j.route, 600, 190);
      gloss(x, 1024, 256);
      save('jeepney_side_' + j.id, c);
      // ---- rear 512x416
      [c, x] = cv(512, 416);
      x.fillStyle = j.body; x.fillRect(0, 0, 512, 416);
      if (j.motif === 'flag') { x.fillStyle = '#0038A8'; x.fillRect(0, 0, 512, 208); x.fillStyle = '#CE1126'; x.fillRect(0, 208, 512, 208); }
      x.fillStyle = j.motif === 'gold' ? '#000000' : '#1B1033'; x.fillRect(0, 0, 512, 70);
      x.textAlign = 'center'; x.textBaseline = 'middle';
      x.fillStyle = j.motif === 'gold' ? '#FFD700' : j.a2 === '#FFFFFF' ? '#FFD23F' : j.a2;
      fit(x, j.rear, 0, 0, 480, 44, (z) => `${z}px ${D}`); x.fillText(j.rear, 256, 38);
      x.fillStyle = '#140F1A'; x.fillRect(150, 80, 212, 336);
      x.fillStyle = '#8B1E2D'; x.fillRect(150, 250, 40, 166); x.fillRect(322, 250, 40, 166);
      x.fillStyle = '#ffe9a8'; x.fillRect(200, 84, 112, 6);
      x.fillStyle = j.a1; x.fillRect(0, 370, 150, 12); x.fillRect(362, 370, 150, 12);
      x.fillStyle = '#fff'; x.fillRect(20, 300, 120, 44); x.strokeStyle = '#0A7B3E'; x.lineWidth = 4; x.strokeRect(20, 300, 120, 44);
      x.fillStyle = '#0A7B3E'; fit(x, j.plate, 0, 0, 108, 28, (z) => `900 ${z}px ${B}`); x.fillText(j.plate, 80, 323);
      gloss(x, 512, 416);
      save('jeepney_rear_' + j.id, c);
      // ---- roof sign 512x128
      [c, x] = cv(512, 128);
      x.fillStyle = j.signBg; x.fillRect(0, 0, 512, 128);
      x.textAlign = 'center'; x.textBaseline = 'middle';
      if (j.led) {
        x.fillStyle = j.signFg; x.shadowColor = j.signFg; x.shadowBlur = 12;
        fit(x, j.sign, 0, 0, 470, 64, (z) => `900 ${z}px ${B}`); x.fillText(j.sign, 256, 66); x.shadowBlur = 0;
        x.fillStyle = 'rgba(0,0,0,0.45)'; for (let yy = 0; yy < 128; yy += 6) x.fillRect(0, yy, 512, 2); for (let xx = 0; xx < 512; xx += 6) x.fillRect(xx, 0, 2, 128);
      } else {
        x.fillStyle = j.a1; x.fillRect(0, 0, 512, 10); x.fillRect(0, 118, 512, 10);
        if (j.motif === 'gold') x.fillStyle = goldGrad(x, 20, 110); else x.fillStyle = j.signFg;
        fit(x, j.sign, 0, 0, 470, 84, (z) => `${z}px ${D}`); x.fillText(j.sign, 256, 70);
      }
      save('jeepney_sign_' + j.id, c);
    }
    function Palette(hex) { // darker outline colour
      const n = parseInt(hex.slice(1), 16); const r = (n >> 16) * 0.45, g = ((n >> 8) & 255) * 0.45, b = (n & 255) * 0.45;
      return `rgb(${r | 0},${g | 0},${b | 0})`;
    }
  }
  // -------------------------------------------------- ground surfaces for the new routes
  {
    let [c, x] = cv(256, 256);
    x.fillStyle = '#6A994E'; x.fillRect(0, 0, 256, 256);
    for (let i = 0; i < 2600; i++) { x.fillStyle = ['#7FB069', '#5A8A3F', '#8DBF6A', '#4F7A36'][i % 4]; x.fillRect(Math.random() * 256, Math.random() * 256, 2, 4 + Math.random() * 5); }
    save('grass', c);
    [c, x] = cv(256, 256);
    x.fillStyle = '#E9D8A6'; x.fillRect(0, 0, 256, 256);
    noise(x, 256, 256, 2200, 0.14, true); noise(x, 256, 256, 800, 0.08);
    for (let i = 0; i < 14; i++) { x.fillStyle = 'rgba(255,255,255,0.7)'; x.beginPath(); x.arc(Math.random() * 256, Math.random() * 256, 2, 0, Math.PI * 2); x.fill(); }
    save('sand', c);
  }
  // -------------------------------------------------- Mountain Province: unpaved, muddy, rutted road
  {
    const W = 512, H = 512, roadW = 11.2;
    let [c, x] = cv(W, H);
    const px = (m) => W / 2 + (m / roadW) * W;
    x.fillStyle = '#7A5A3A'; x.fillRect(0, 0, W, H);
    noise(x, W, H, 5000, 0.22); noise(x, W, H, 1600, 0.12, true);
    // darker damp patches and dried lighter crust
    for (let i = 0; i < 26; i++) { x.fillStyle = Math.random() < 0.6 ? 'rgba(60,40,24,0.35)' : 'rgba(160,130,95,0.25)'; x.beginPath(); x.ellipse(Math.random() * W, Math.random() * H, 14 + Math.random() * 46, 20 + Math.random() * 70, Math.random() * 3, 0, Math.PI * 2); x.fill(); }
    // wheel ruts: two per lane, wet and dark, slightly wavy
    for (const lx of [-3.2, 0, 3.2]) for (const off of [-0.85, 0.85]) {
      x.beginPath();
      for (let y = 0; y <= H; y += 8) { const wob = Math.sin((y / H) * Math.PI * 2 + lx * 3 + off) * 7 + Math.sin((y / H) * Math.PI * 6 + off) * 2; y === 0 ? x.moveTo(px(lx + off) + wob, y) : x.lineTo(px(lx + off) + wob, y); }
      x.strokeStyle = 'rgba(70,48,28,0.28)'; x.lineWidth = 30; x.stroke();   // soft trough
      x.strokeStyle = 'rgba(45,30,16,0.35)'; x.lineWidth = 12; x.stroke();   // packed wheel track
      x.strokeStyle = 'rgba(120,140,150,0.10)'; x.lineWidth = 4; x.stroke(); // damp glint
    }
    // muddy puddles
    for (let i = 0; i < 5; i++) { const cx = px(-4.5 + Math.random() * 9), cy = Math.random() * H; x.fillStyle = 'rgba(52,40,30,0.75)'; x.beginPath(); x.ellipse(cx, cy, 16 + Math.random() * 22, 26 + Math.random() * 34, Math.random(), 0, Math.PI * 2); x.fill(); x.fillStyle = 'rgba(150,170,180,0.25)'; x.beginPath(); x.ellipse(cx - 4, cy - 6, 8, 12, 0.3, 0, Math.PI * 2); x.fill(); }
    // gravel and stones
    for (let i = 0; i < 700; i++) { const g = 95 + Math.random() * 45 | 0; x.fillStyle = `rgb(${g},${g - 14},${g - 32})`; x.fillRect(Math.random() * W, Math.random() * H, 2 + Math.random() * 3, 2 + Math.random() * 3); }
    // grassy edges
    for (const side of [-1, 1]) for (let i = 0; i < 260; i++) { x.fillStyle = ['#5A7F3A', '#4F7334', '#6B8F45'][i % 3]; x.fillRect(px(side * (5.0 + Math.random() * 0.6)), Math.random() * H, 2, 5 + Math.random() * 7); }
    save('road_dirt', c);
    // shoulder: red-brown soil with grass tufts and stones
    [c, x] = cv(256, 256);
    x.fillStyle = '#6E5236'; x.fillRect(0, 0, 256, 256); noise(x, 256, 256, 2400, 0.18);
    for (let i = 0; i < 900; i++) { x.fillStyle = ['#557A35', '#4A6B2E', '#6A8F43'][i % 3]; x.fillRect(Math.random() * 256, Math.random() * 256, 2, 4 + Math.random() * 6); }
    for (let i = 0; i < 40; i++) { const g = 110 + Math.random() * 60 | 0; x.fillStyle = `rgb(${g},${g - 6},${g - 18})`; x.beginPath(); x.ellipse(Math.random() * 256, Math.random() * 256, 3 + Math.random() * 5, 2 + Math.random() * 4, Math.random() * 3, 0, Math.PI * 2); x.fill(); }
    save('dirt', c);
    // edge: rough earth bank with rocks (replaces the painted curb)
    [c, x] = cv(256, 32);
    x.fillStyle = '#5E4430'; x.fillRect(0, 0, 256, 32); noise(x, 256, 32, 500, 0.3);
    for (let i = 0; i < 24; i++) { const g = 100 + Math.random() * 70 | 0; x.fillStyle = `rgb(${g},${g - 8},${g - 20})`; x.beginPath(); x.ellipse(Math.random() * 256, 8 + Math.random() * 18, 4 + Math.random() * 7, 3 + Math.random() * 5, 0, 0, Math.PI * 2); x.fill(); }
    save('curb_dirt', c);
  }
  // -------------------------------------------------- Phase 2 UI icons
  {
    const icon = (name, w, h, draw) => { const [c, x] = cv(w, h); draw(x, w, h); save(name, c); };
    const disc = (x, col) => { x.fillStyle = col; x.strokeStyle = '#1b1033'; x.lineWidth = 8; x.beginPath(); x.arc(64, 64, 56, 0, Math.PI * 2); x.fill(); x.stroke(); };
    icon('ui_pu_Magnet', 128, 128, (x) => { disc(x, '#FFE3E8'); x.lineWidth = 22; x.strokeStyle = '#E63946'; x.beginPath(); x.arc(64, 58, 26, Math.PI, 0); x.stroke(); x.fillStyle = '#E63946'; x.fillRect(27, 58, 22, 22); x.fillRect(79, 58, 22, 22); x.fillStyle = '#ddd'; x.fillRect(27, 80, 22, 14); x.fillRect(79, 80, 22, 14); });
    icon('ui_pu_Shield', 128, 128, (x) => { disc(x, '#DFF6FF'); x.fillStyle = '#4CC9F0'; x.strokeStyle = '#1b1033'; x.lineWidth = 6; x.beginPath(); x.moveTo(64, 22); x.lineTo(98, 36); x.quadraticCurveTo(98, 86, 64, 106); x.quadraticCurveTo(30, 86, 30, 36); x.closePath(); x.fill(); x.stroke(); x.fillStyle = '#fff'; x.fillRect(58, 42, 12, 44); x.fillRect(44, 56, 40, 12); });
    icon('ui_pu_Turbo', 128, 128, (x) => { disc(x, '#FFF1DB'); x.fillStyle = '#FF9F1C'; x.strokeStyle = '#1b1033'; x.lineWidth = 6; x.beginPath(); x.moveTo(72, 18); x.lineTo(36, 70); x.lineTo(60, 70); x.lineTo(52, 110); x.lineTo(92, 54); x.lineTo(68, 54); x.closePath(); x.fill(); x.stroke(); });
    icon('ui_pu_DoubleCoins', 128, 128, (x) => { disc(x, '#FFF6CC'); x.fillStyle = '#FFC928'; x.strokeStyle = '#8a5a00'; x.lineWidth = 6; x.beginPath(); x.arc(54, 70, 28, 0, Math.PI * 2); x.fill(); x.stroke(); x.beginPath(); x.arc(76, 54, 28, 0, Math.PI * 2); x.fill(); x.stroke(); x.fillStyle = '#1b1033'; x.textAlign = 'center'; x.textBaseline = 'middle'; x.font = `44px ${D}`; x.fillText('x2', 76, 58); });
    icon('ui_pu_PassengerBonus', 128, 128, (x) => { disc(x, '#DDFBEA'); x.fillStyle = '#2A9D8F'; x.beginPath(); x.arc(56, 44, 16, 0, Math.PI * 2); x.fill(); x.beginPath(); x.roundRect(32, 64, 48, 40, 14); x.fill(); x.fillStyle = '#18C06A'; x.fillRect(86, 46, 12, 36); x.fillRect(74, 58, 36, 12); });
    icon('ui_pu_RepairKit', 128, 128, (x) => { disc(x, '#FFE0DE'); x.fillStyle = '#D62828'; x.strokeStyle = '#1b1033'; x.lineWidth = 6; x.beginPath(); x.roundRect(26, 38, 76, 56, 10); x.fill(); x.stroke(); x.fillStyle = '#fff'; x.fillRect(56, 46, 16, 40); x.fillRect(44, 58, 40, 16); });
    icon('ui_gas', 128, 128, (x) => { x.fillStyle = '#1b1033'; x.beginPath(); x.moveTo(64, 14); x.lineTo(104, 64); x.lineTo(80, 64); x.lineTo(80, 112); x.lineTo(48, 112); x.lineTo(48, 64); x.lineTo(24, 64); x.closePath(); x.fill(); });
    icon('ui_brake', 128, 128, (x) => { x.fillStyle = '#1b1033'; x.beginPath(); for (let i = 0; i < 8; i++) { const a = Math.PI / 8 + i * Math.PI / 4; x.lineTo(64 + Math.cos(a) * 54, 64 + Math.sin(a) * 54); } x.fill(); x.fillStyle = '#fff'; x.textAlign = 'center'; x.textBaseline = 'middle'; x.font = `34px ${D}`; x.fillText('STOP', 64, 68); });
    icon('ui_lock', 128, 128, (x) => { x.strokeStyle = '#1b1033'; x.lineWidth = 14; x.beginPath(); x.arc(64, 52, 24, Math.PI, 0); x.stroke(); x.fillStyle = '#1b1033'; x.beginPath(); x.roundRect(28, 52, 72, 56, 10); x.fill(); x.fillStyle = '#FCD116'; x.beginPath(); x.arc(64, 78, 9, 0, Math.PI * 2); x.fill(); });
    icon('ui_check', 128, 128, (x) => { disc(x, '#18C06A'); x.strokeStyle = '#fff'; x.lineWidth = 16; x.lineCap = 'round'; x.beginPath(); x.moveTo(38, 66); x.lineTo(56, 86); x.lineTo(92, 44); x.stroke(); });
    icon('ui_trophy', 128, 128, (x) => { x.fillStyle = '#FFC928'; x.strokeStyle = '#1b1033'; x.lineWidth = 6; x.beginPath(); x.moveTo(34, 20); x.lineTo(94, 20); x.lineTo(90, 60); x.quadraticCurveTo(64, 84, 38, 60); x.closePath(); x.fill(); x.stroke(); x.fillRect(56, 76, 16, 20); x.beginPath(); x.roundRect(36, 96, 56, 16, 4); x.fill(); x.stroke(); x.beginPath(); x.arc(28, 36, 12, 0.5 * Math.PI, 1.6 * Math.PI); x.stroke(); x.beginPath(); x.arc(100, 36, 12, 1.4 * Math.PI, 0.5 * Math.PI); x.stroke(); });
    icon('ui_star', 128, 128, (x) => { star(x, 64, 66, 54, '#1b1033'); star(x, 64, 66, 44, '#FFD23F'); });
    // route thumbnails 360x200
    icon('ui_route_manila', 360, 200, (x, w, h) => { const g = x.createLinearGradient(0, 0, 0, h); g.addColorStop(0, '#9fd3f7'); g.addColorStop(1, '#ffe3b8'); x.fillStyle = g; x.fillRect(0, 0, w, h); const cols = ['#F6BD60', '#F28482', '#BDE0FE', '#CDB4DB', '#84A59D']; for (let i = 0; i < 9; i++) { x.fillStyle = cols[i % 5]; const bh = 60 + (i * 37) % 90; x.fillRect(i * 40, h - 40 - bh, 38, bh); x.fillStyle = 'rgba(30,50,80,0.6)'; for (let r = 0; r < bh / 22 - 1; r++) x.fillRect(i * 40 + 8, h - 40 - bh + 10 + r * 22, 22, 10); } x.fillStyle = '#45474e'; x.fillRect(0, h - 40, w, 40); x.fillStyle = '#fff'; for (let i = 0; i < 9; i++) x.fillRect(i * 44 + 10, h - 22, 22, 4); });
    icon('ui_route_provincial', 360, 200, (x, w, h) => { x.fillStyle = '#b8e0f6'; x.fillRect(0, 0, w, h); x.fillStyle = '#6C9A8B'; for (const [mx, mh] of [[60, 90], [170, 120], [290, 80]]) { x.beginPath(); x.moveTo(mx - 90, 130); x.lineTo(mx, 130 - mh); x.lineTo(mx + 90, 130); x.fill(); } x.fillStyle = '#7CB518'; x.fillRect(0, 120, w, 80); x.strokeStyle = '#5C9E1F'; x.lineWidth = 3; for (let i = 0; i < 12; i++) { x.beginPath(); x.moveTo(0, 130 + i * 6); x.lineTo(w, 126 + i * 6); x.stroke(); } x.fillStyle = '#8D6E4F'; x.fillRect(270, 70, 8, 70); x.fillStyle = '#2D6A4F'; for (let i = 0; i < 6; i++) { x.save(); x.translate(274, 70); x.rotate(i * 1.05); x.fillRect(0, -4, 40, 8); x.restore(); } x.fillStyle = '#D4A373'; x.fillRect(60, 100, 50, 30); x.fillStyle = '#9C7A3C'; x.beginPath(); x.moveTo(50, 102); x.lineTo(85, 75); x.lineTo(120, 102); x.fill(); });
    icon('ui_route_mountain', 360, 200, (x, w, h) => { const g = x.createLinearGradient(0, 0, 0, h); g.addColorStop(0, '#9bb7c9'); g.addColorStop(1, '#dfe8e4'); x.fillStyle = g; x.fillRect(0, 0, w, h); x.fillStyle = '#5E7F74'; for (const [mx, mh] of [[40, 110], [150, 150], [270, 125], [350, 95]]) { x.beginPath(); x.moveTo(mx - 110, 150); x.lineTo(mx, 150 - mh); x.lineTo(mx + 110, 150); x.fill(); } x.fillStyle = 'rgba(255,255,255,0.45)'; x.fillRect(0, 95, w, 18); x.fillStyle = '#4F7A3A'; x.fillRect(0, 115, w, h - 115); x.fillStyle = '#6A994E'; for (let i = 0; i < 5; i++) x.fillRect(0, 118 + i * 8, w, 6); x.fillStyle = '#7A5A3A'; x.beginPath(); x.moveTo(140, h); x.lineTo(175, 150); x.lineTo(195, 150); x.lineTo(240, h); x.fill(); x.strokeStyle = '#4A3420'; x.lineWidth = 3; x.beginPath(); x.moveTo(160, h); x.lineTo(181, 150); x.moveTo(220, h); x.lineTo(190, 150); x.stroke(); x.fillStyle = '#1F4D3A'; for (const [tx, th] of [[40, 70], [70, 55], [300, 75], [330, 58], [110, 45], [265, 50]]) { x.beginPath(); x.moveTo(tx - 14, h - 10); x.lineTo(tx, h - 10 - th); x.lineTo(tx + 14, h - 10); x.fill(); } });
    icon('ui_route_beach', 360, 200, (x, w, h) => { x.fillStyle = '#7fd6f7'; x.fillRect(0, 0, w, h); x.fillStyle = '#FFF3B0'; x.beginPath(); x.arc(290, 50, 26, 0, Math.PI * 2); x.fill(); x.fillStyle = '#1F7EA1'; x.fillRect(0, 100, w, 60); x.fillStyle = '#4CC9F0'; x.fillRect(0, 140, w, 20); x.fillStyle = '#E9D8A6'; x.fillRect(0, 160, w, 40); x.fillStyle = '#fff'; x.fillRect(90, 120, 60, 10); x.fillStyle = '#EF476F'; x.fillRect(95, 112, 50, 10); x.fillStyle = '#8D6E4F'; x.fillRect(40, 90, 8, 80); x.fillStyle = '#40916C'; for (let i = 0; i < 6; i++) { x.save(); x.translate(44, 90); x.rotate(i * 1.05); x.fillRect(0, -4, 38, 8); x.restore(); } });
  }
  // -------------------------------------------------- soft particle dot + MMDA sign + speech bubble
  {
    let [c, x] = cv(64, 64);
    const g = x.createRadialGradient(32, 32, 0, 32, 32, 32); g.addColorStop(0, 'rgba(255,255,255,1)'); g.addColorStop(0.45, 'rgba(255,255,255,0.75)'); g.addColorStop(1, 'rgba(255,255,255,0)');
    x.fillStyle = g; x.fillRect(0, 0, 64, 64);
    save('particle_dot', c);
    [c, x] = cv(64, 64);
    x.translate(32, 32); x.fillStyle = '#fff'; x.beginPath(); for (let i = 0; i < 8; i++) { const a = i * Math.PI / 4, r = i % 2 ? 9 : 31; x.lineTo(Math.cos(a) * r, Math.sin(a) * r); } x.fill();
    save('particle_star', c);
    [c, x] = cv(512, 256);
    x.fillStyle = '#0038A8'; x.fillRect(0, 0, 512, 256); x.strokeStyle = '#FFFFFF'; x.lineWidth = 10; x.strokeRect(8, 8, 496, 240);
    x.textAlign = 'center'; x.textBaseline = 'middle';
    x.fillStyle = '#FFFFFF'; fit(x, 'MMDA', 0, 0, 440, 110, (z) => `${z}px ${D}`); x.fillText('MMDA', 256, 96);
    x.fillStyle = '#FCD116'; fit(x, 'CHECKPOINT', 0, 0, 460, 64, (z) => `${z}px ${D}`); x.fillText('CHECKPOINT', 256, 186);
    save('mmda_sign', c);
    [c, x] = cv(256, 128);
    x.fillStyle = '#FFFFFF'; x.strokeStyle = '#1b1033'; x.lineWidth = 8; x.beginPath(); x.roundRect(6, 6, 244, 96, 36); x.fill(); x.stroke();
    x.beginPath(); x.moveTo(60, 98); x.lineTo(48, 124); x.lineTo(92, 98); x.closePath(); x.fill(); x.stroke();
    x.fillStyle = '#FFFFFF'; x.fillRect(56, 92, 32, 9);
    save('ui_bubble', c);
  }
  return out;
});

for (const [name, url] of Object.entries(textures)) {
  writeFileSync(join(out, name + '.png'), Buffer.from(url.split(',')[1], 'base64'));
  console.log('wrote', name + '.png');
}
await browser.close();
