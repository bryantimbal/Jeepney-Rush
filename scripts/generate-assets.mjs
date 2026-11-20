/**
 * Renders the launcher icons, Android splash images and Play Store graphics
 * from public/icon.svg using headless Chromium (Playwright).
 *
 *   node scripts/generate-assets.mjs
 */
import { createRequire } from 'node:module';
import { execSync } from 'node:child_process';
import { readFileSync, readdirSync, existsSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

const require = createRequire(import.meta.url);
let pw;
try {
  pw = require('playwright');
} catch {
  pw = require(`${execSync('npm root -g').toString().trim()}/playwright`);
}
const root = new URL('..', import.meta.url).pathname;
const svg = readFileSync(join(root, 'public/icon.svg'), 'utf8');
const inner = svg
  .replace(/^[\s\S]*?<svg[^>]*>/, '')
  .replace(/<\/svg>\s*$/, '')
  .replaceAll('font-family="Arial Black, Impact, sans-serif"', 'font-family="Luckiest Guy, Arial Black, Impact, sans-serif"');
const fontUrl = 'file://' + join(root, 'node_modules/@fontsource/luckiest-guy/files/luckiest-guy-latin-400-normal.woff2');
const res = join(root, 'android/app/src/main/res');

const browser = await pw.chromium.launch();
const page = await browser.newPage();
await page.goto('file://' + join(root, 'package.json'));

async function render(html, w, h, out, transparent = false) {
  await page.setViewportSize({ width: w, height: h });
  await page.setContent(`<!doctype html><html><head><style>@font-face{font-family:'Luckiest Guy';src:url(${fontUrl}) format('woff2')}html,body{margin:0;width:${w}px;height:${h}px;overflow:hidden;background:${transparent ? 'transparent' : '#1b1033'}}</style></head><body>${html}</body></html>`);
  await page.evaluate(() => document.fonts.ready);
  await page.screenshot({ path: out, omitBackground: transparent });
}

const iconSvg = (size, round = false) => `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512" width="${size}" height="${size}">${round ? '<clipPath id="c"><circle cx="256" cy="256" r="256"/></clipPath><g clip-path="url(#c)">' : ''}${inner.replace('rx="110"', round ? 'rx="0"' : 'rx="110"')}${round ? '</g>' : ''}</svg>`;
// adaptive foreground: artwork inside the 66% safe zone, transparent background
const fgSvg = (size) => `<svg xmlns="http://www.w3.org/2000/svg" viewBox="-128 -128 768 768" width="${size}" height="${size}">${inner.replace(/<rect width="512" height="512" rx="110" fill="url\(#bg\)"\/>/, '')}</svg>`;

const densities = { mdpi: 1, hdpi: 1.5, xhdpi: 2, xxhdpi: 3, xxxhdpi: 4 };
if (existsSync(res)) {
  for (const [d, k] of Object.entries(densities)) {
    const dir = join(res, `mipmap-${d}`);
    await render(iconSvg(48 * k), 48 * k, 48 * k, join(dir, 'ic_launcher.png'), true);
    await render(iconSvg(48 * k, true), 48 * k, 48 * k, join(dir, 'ic_launcher_round.png'), true);
    await render(fgSvg(108 * k), 108 * k, 108 * k, join(dir, 'ic_launcher_foreground.png'), true);
  }
  writeFileSync(join(res, 'values/ic_launcher_background.xml'), '<?xml version="1.0" encoding="utf-8"?>\n<resources>\n    <color name="ic_launcher_background">#FF6A3D</color>\n</resources>\n');
  // splash screens: keep each existing file's dimensions
  for (const dir of readdirSync(res).filter((d) => d.startsWith('drawable'))) {
    const file = join(res, dir, 'splash.png');
    if (!existsSync(file)) continue;
    const buf = readFileSync(file);
    const w = buf.readUInt32BE(16), h = buf.readUInt32BE(20);
    const s = Math.round(Math.min(w, h) * 0.45);
    await render(`<div style="width:${w}px;height:${h}px;display:flex;align-items:center;justify-content:center;background:radial-gradient(circle at 50% 40%,#ff6a3d,#7b1fa2 70%,#1b1033)">${iconSvg(s)}</div>`, w, h, file);
  }
  console.log('Android icons + splash written');
}

await render(iconSvg(512), 512, 512, join(root, 'store/icon-512.png'));
await render(
  `<div style="width:1024px;height:500px;display:flex;align-items:center;gap:40px;padding:0 60px;box-sizing:border-box;background:radial-gradient(circle at 30% 40%,#ffb347,#ff6a3d 40%,#7b1fa2 85%);font-family:'Luckiest Guy','Arial Black',Impact,sans-serif">
    ${iconSvg(360)}
    <div style="color:#fff;text-shadow:0 6px 0 #1b1033">
      <div style="font-size:92px;line-height:.95;color:#fcd116;-webkit-text-stroke:4px #1b1033;paint-order:stroke fill">JEEPNEY</div>
      <div style="font-size:120px;line-height:.95;-webkit-text-stroke:5px #1b1033;paint-order:stroke fill">RUSH</div>
      <div style="margin-top:18px;font-size:24px;background:#fcd116;color:#1b1033;display:inline-block;padding:6px 18px;border-radius:999px;border:4px solid #1b1033;text-shadow:none;font-family:Arial,sans-serif;font-weight:900;white-space:nowrap">The Ultimate Pinoy Driving Challenge!</div>
    </div>
  </div>`,
  1024,
  500,
  join(root, 'store/feature-graphic-1024x500.png'),
);
console.log('Store graphics written to store/');
await browser.close();
