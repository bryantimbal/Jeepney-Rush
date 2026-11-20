/**
 * End-to-end smoke test: boots the real game in headless Chromium, plays it
 * with keyboard input and checks gameplay, UI flow and save persistence.
 *
 *   npm run test:e2e            (uses the Vite dev server on a free port)
 *   SHOTS=1 npm run test:e2e    (also writes screenshots to tests/e2e/out)
 */
import { createRequire } from 'node:module';
import { execSync } from 'node:child_process';
import { mkdirSync } from 'node:fs';
import { createServer } from 'vite';

const require = createRequire(import.meta.url);
function loadPlaywright() {
  try {
    return require('playwright');
  } catch {
    const globalRoot = execSync('npm root -g').toString().trim();
    return require(`${globalRoot}/playwright`);
  }
}
const { chromium } = loadPlaywright();

const SHOTS = !!process.env.SHOTS;
const outDir = new URL('./out/', import.meta.url).pathname;
if (SHOTS) mkdirSync(outDir, { recursive: true });

let failures = 0;
const check = (name, ok, detail = '') => {
  console.log(`${ok ? '  ✔' : '  ✘'} ${name}${detail ? ` — ${detail}` : ''}`);
  if (!ok) failures++;
};

const server = await createServer({ server: { port: 0, host: '127.0.0.1' }, logLevel: 'error' });
await server.listen();
const port = server.httpServer.address().port;
const base = `http://127.0.0.1:${port}/`;

const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--autoplay-policy=no-user-gesture-required'] });
const context = await browser.newContext({ viewport: { width: 405, height: 720 }, deviceScaleFactor: 1 });
const page = await context.newPage();
const errors = [];
page.on('pageerror', (e) => errors.push(e.message));
page.on('console', (m) => m.type() === 'error' && errors.push(m.text()));

const state = () => page.evaluate(() => window.__jr.state());
const shot = async (name) => SHOTS && page.screenshot({ path: `${outDir}${name}.png` });
const waitFor = async (fn, timeout = 20000) => {
  const t0 = Date.now();
  while (Date.now() - t0 < timeout) {
    if (await fn()) return true;
    await page.waitForTimeout(100);
  }
  return false;
};

try {
  console.log('Boot');
  await page.goto(`${base}?seed=11`);
  check('splash shows TAP TO START', await waitFor(() => page.isVisible('#btnTapStart')));
  await shot('01-splash');
  await page.click('#btnTapStart', { force: true });
  check('main menu visible', await waitFor(() => page.isVisible('#btnPlay')));
  await page.waitForTimeout(800);
  await shot('02-menu');

  console.log('Menus');
  await page.click('#btnSettings');
  check('settings opens', await page.isVisible('#settings .card'));
  await page.click('#sQuality button[data-q="high"]');
  check('quality toggles', (await page.getAttribute('#sQuality button[data-q="high"]', 'class'))?.includes('on'));
  await page.click('#btnSettingsBack');
  await page.click('#btnHow');
  check('how-to opens', await page.isVisible('#howto .card'));
  await page.click('#btnHowBack');
  await page.click('#btnStats');
  check('records opens', await page.isVisible('#stats .card'));
  await page.click('#btnStatsBack');

  console.log('Run');
  await page.click('#btnPlay');
  check('run starts', await waitFor(async () => (await state()).state === 'playing'));
  await page.waitForTimeout(1500);
  let s = await state();
  check('jeepney moves forward', s.distance > 5 && s.speed > 5, `d=${s.distance.toFixed(1)} v=${s.speed.toFixed(1)}`);
  check('traffic spawned', s.hazards > 0, `hazards=${s.hazards}`);
  check('coins spawned', await waitFor(async () => (await state()).coinsActive > 0, 8000));

  await page.keyboard.press('ArrowLeft');
  await page.waitForTimeout(400);
  s = await state();
  check('swipe-left (←) changes lane', s.lane === 0, `lane=${s.lane}`);
  await page.keyboard.press('ArrowRight');
  await page.keyboard.press('ArrowRight');
  await page.waitForTimeout(600);
  s = await state();
  check('double ← → buffering reaches right lane', s.lane === 2, `lane=${s.lane}`);
  await page.keyboard.press('Space');
  await page.waitForTimeout(120);
  const airborne = await page.evaluate(() => window.__jr.game.player.y);
  check('space jumps', airborne > 0.3, `y=${airborne.toFixed(2)}`);
  await page.waitForTimeout(800);
  const v0 = (await state()).speed;
  await page.keyboard.press('ArrowDown');
  await page.waitForTimeout(500);
  const v1 = (await state()).speed;
  check('↓ brakes', v1 < v0 - 1, `${v0.toFixed(1)} → ${v1.toFixed(1)} m/s`);
  await page.keyboard.down('Shift');
  await page.waitForTimeout(1500);
  const gas = await page.evaluate(() => window.__jr.game.run.gas);
  check('holding Shift steps on the gas', gas === true);
  await page.keyboard.up('Shift');
  await shot('03-run');

  console.log('Pause');
  await page.keyboard.press('Escape');
  check('Escape pauses', await waitFor(async () => (await state()).state === 'paused', 2000));
  const dPaused = (await state()).distance;
  await page.waitForTimeout(700);
  check('world frozen while paused', (await state()).distance === dPaused);
  await shot('04-pause');
  await page.click('#btnResume');
  check('resume', await waitFor(async () => (await state()).state === 'playing', 2000));

  console.log('Passengers (autopilot to stops)');
  // steer toward the lane of the next stop; dodge nothing – we only check the stop logic fires
  const t0 = Date.now();
  let served = false;
  while (Date.now() - t0 < 60000) {
    const info = await page.evaluate(() => {
      const g = window.__jr.game;
      const ns = g.nextStop();
      return { st: g.state, ns, lane: g.player.lane, picked: g.world.route.pickedUp, health: g.player.health };
    });
    if (info.st !== 'playing') break;
    if (info.picked > 0) {
      served = true;
      break;
    }
    if (info.ns) {
      const want = info.ns.side < 0 ? 0 : 2;
      if (info.lane < want) await page.keyboard.press('ArrowRight');
      else if (info.lane > want) await page.keyboard.press('ArrowLeft');
    }
    // keep the run alive for the test: refill health (test-only)
    await page.evaluate(() => {
      const g = window.__jr.game;
      g.player.health = g.player.maxHealth;
      g.run.fuel = 100;
    });
    await page.waitForTimeout(150);
  }
  s = await state();
  check('passengers picked up at a PARA stop', served, `picked=${s.pickedUp} aboard=${s.passengers}`);
  check('coins collected during run', s.coins > 0, `coins=${s.coins}`);
  await shot('05-passengers');

  console.log('Game over');
  // let traffic end the run (no more test healing)
  const over = await waitFor(async () => (await state()).state === 'gameover', 120000);
  check('run ends in game over', over);
  await page.waitForTimeout(500);
  check('game over card visible', await page.isVisible('#gameover .card'));
  const goScore = await page.textContent('#goScore');
  check('final score shown', Number(goScore.replace(/[^0-9]/g, '')) > 0, goScore);
  await shot('06-gameover');

  console.log('Persistence');
  await page.click('#btnHome');
  check('home returns to menu', await waitFor(() => page.isVisible('#btnPlay')));
  const best = await page.textContent('#menuBest');
  await page.reload();
  await page.waitForSelector('#btnTapStart', { state: 'visible' });
  await page.click('#btnTapStart', { force: true });
  await page.waitForSelector('#btnPlay', { state: 'visible' });
  const bestAfter = await page.textContent('#menuBest');
  check('best score persisted across reload', best === bestAfter && bestAfter !== '0', `${best} / ${bestAfter}`);
  const quality = await page.evaluate(() => window.__jr.game.save.data.settings.quality);
  check('settings persisted across reload', quality === 'high');

  check('no runtime errors', errors.length === 0, errors.slice(0, 3).join(' | '));
} catch (e) {
  console.error(e);
  failures++;
} finally {
  await browser.close();
  await server.close();
}

console.log(failures ? `\n${failures} check(s) failed` : '\nAll e2e checks passed');
process.exit(failures ? 1 : 0);
