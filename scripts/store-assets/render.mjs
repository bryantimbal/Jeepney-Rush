// Renders the Google Play store assets from the game's own models and textures.
//   dotnet run --project unity/Tools.Preview -- unity/Tools.Preview/bin/pv   (models.json, once per art change)
//   node scripts/store-assets/render.mjs && python3 scripts/store-assets/compose.py
// Raw 3D renders go to scripts/store-assets/.raw (git-ignored); final assets to store/play/.
import { createRequire } from 'node:module';
import { execSync } from 'node:child_process';
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const require = createRequire(import.meta.url);
let pw; try { pw = require('playwright'); } catch { pw = require(`${execSync('npm root -g').toString().trim()}/playwright`); }
const { chromium } = pw;
const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, '../..');
const raw = path.join(here, '.raw');
fs.mkdirSync(raw, { recursive: true });
const types = { '.html': 'text/html', '.js': 'text/javascript', '.json': 'application/json', '.png': 'image/png', '.ttf': 'font/ttf' };
const server = http.createServer((req, res) => {
  const p = path.join(root, decodeURIComponent(new URL(req.url, 'http://x').pathname));
  if (!p.startsWith(root) || !fs.existsSync(p) || fs.statSync(p).isDirectory()) { res.writeHead(404); res.end(); return; }
  res.writeHead(200, { 'Content-Type': types[path.extname(p)] || 'application/octet-stream' }); fs.createReadStream(p).pipe(res);
});
await new Promise(r => server.listen(0, '127.0.0.1', r));
const base = `http://127.0.0.1:${server.address().port}/scripts/store-assets/scene.html`;

// screenshot scenes (rendered at the inset size of each device class; all insets are 2:3)
export const shots = [
  ['01_stop', 'mode=stop&cam=1.4,7,12.5,3.2,1.2,-16,55'],
  ['02_edsa', 'mode=edsa&cam=1.4,8,13,1.0,1.2,-16,55'],
  ['03_driver', 'mode=driver'],
  ['04_night', 'mode=night&night=1&rain=1'],
  ['05_street', 'mode=corner'],
  ['06_garage', 'mode=garage&cam=0,5.6,12.5,0,1.3,-2,52'],
];
const insets = { phone: [960, 1440], tablet7: [1024, 1536], tablet10: [1280, 1920] };
const jobs = [
  ['icon', 'mode=icon&cam=-5.0,1.3,-8.0,-0.9,1.6,-0.8,48', 1024, 1024, true],
  ['feature', 'mode=hero&cam=-3.2,1.9,-17.5,-6.2,2.0,-4,36', 2048, 1000, false],
];
for (const [dev, [w, h]] of Object.entries(insets)) for (const [name, q] of shots) jobs.push([`${dev}_${name}`, q, w, h, false]);

const only = process.argv[2] ? new RegExp(process.argv[2]) : null; // e.g. node render.mjs garage
const browser = await chromium.launch({ args: ['--ignore-gpu-blocklist'] });
for (const [name, q, w, h, transparent] of jobs) {
  if (only && !only.test(name)) continue;
  const page = await browser.newPage({ viewport: { width: w, height: h }, deviceScaleFactor: 1 });
  page.on('pageerror', e => console.log('error', name, e.message));
  await page.goto(`${base}?${q}`);
  await page.waitForFunction(() => document.title === 'ready', null, { timeout: 120000 });
  await page.screenshot({ path: path.join(raw, name + '.png'), omitBackground: transparent });
  await page.close();
  console.log('rendered', name, `${w}x${h}`);
}
await browser.close();
server.close();
