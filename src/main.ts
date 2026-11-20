import '@fontsource/luckiest-guy/400.css';
import '@fontsource/nunito/700.css';
import '@fontsource/nunito/800.css';
import '@fontsource/nunito/900.css';
import './ui/styles.css';

import { LANES, ROAD_HALF_WIDTH } from './config';
import { Renderer } from './render/Renderer';
import { materials } from './art/Materials';
import { VehicleLibrary } from './art/VehicleFactory';
import { buildJeepney } from './art/JeepneyFactory';
import { CLASSIC_SKIN } from './art/JeepneySkins';
import { World } from './game/World';
import { PlayerController } from './game/PlayerController';
import { AudioManager } from './audio/AudioManager';
import { SaveManager } from './save/SaveManager';
import { Input } from './core/Input';
import { GameManager } from './core/GameManager';
import { UIManager } from './ui/UIManager';
import { PlatformStore, initPlatform, exitApp, isNative } from './platform/Platform';

const nextFrame = () => new Promise<void>((r) => requestAnimationFrame(() => r()));

async function boot(): Promise<void> {
  const store = new PlatformStore();
  await store.hydrate(['jeepneyRush.save', 'jeepneyRush.save.bak']);
  const save = new SaveManager(store);
  const audio = new AudioManager();
  audio.setMusic(save.data.settings.music);
  audio.setSfx(save.data.settings.sfx);

  const stage = document.getElementById('stage')!;
  const renderer = new Renderer(stage);
  const ui = new UIManager(audio, save, renderer);
  const step = async (p: number, text: string) => {
    ui.setLoading(p, text);
    await nextFrame();
  };

  await step(0.05, 'Loading fonts…');
  try {
    await Promise.race([
      Promise.all([document.fonts.load('40px "Luckiest Guy"'), document.fonts.load('900 20px "Nunito"'), document.fonts.load('800 20px "Nunito"')]),
      new Promise((r) => setTimeout(r, 2500)),
    ]);
  } catch {
    /* system fallback fonts */
  }

  await step(0.2, 'Painting the jeepney…');
  materials.init(renderer.renderer, ROAD_HALF_WIDTH * 2, LANES);
  // shadow casting is always configured; the renderer enables/disables shadow maps per quality level
  const jeep = buildJeepney(CLASSIC_SKIN, { shadows: true });
  renderer.scene.add(jeep.root);

  await step(0.4, 'Calling the traffic…');
  const lib = new VehicleLibrary();
  lib.build(true);

  await step(0.6, 'Building Manila streets…');
  const world = new World(renderer.scene, lib, 1234);
  const player = new PlayerController(jeep);

  await step(0.8, 'Warming up the engine…');
  const input = new Input(stage);
  const game = new GameManager(renderer, world, player, audio, save, input);
  game.ui = ui;
  const params = new URLSearchParams(location.search);
  if (params.has('seed')) game.seed = Number(params.get('seed')) || 1;
  world.reset(4321, 5);
  player.reset();
  renderer.setQuality(save.data.settings.quality);
  // compile shaders up-front so the first run has no hitch
  renderer.renderer.compile(renderer.scene, renderer.camera);

  let started = false;
  const start = () => {
    if (started) return;
    started = true;
    audio.unlock();
    audio.playMusic('menu');
    game.toMenu();
  };
  ui.attach(game, start);

  await initPlatform({
    pause: () => {
      game.pause();
      audio.suspend();
    },
    resume: () => audio.resume(),
    back: () => {
      if (ui.back()) return;
      if (game.back()) return;
      if (game.state === 'gameover') {
        game.toMenu();
        return;
      }
      if (game.state === 'menu') void exitApp();
    },
  });
  window.addEventListener('keydown', (e) => {
    if (e.code === 'Escape' && game.state !== 'playing' && game.state !== 'paused') ui.back();
  });

  // main loop
  const loop = (t: number) => {
    try {
      game.frame(t);
    } catch (err) {
      console.error(err);
      ui.fatal(String((err as Error)?.message ?? err));
      return;
    }
    requestAnimationFrame(loop);
  };
  requestAnimationFrame(loop);

  await step(1, 'Ready!');
  if (save.recovered === 'backup') console.warn('[save] main save was corrupted – restored from backup');
  ui.showTapToStart();
  // any tap on the splash starts (unlocks audio as part of the gesture)
  document.getElementById('splash')!.addEventListener('pointerup', start);
  if (params.has('autostart')) start();

  // expose a small debug/testing hook (no game-state mutation from outside except via inputs)
  (window as unknown as { __jr: unknown }).__jr = { game, isNative, state: () => game.debugState() };
}

boot().catch((err) => {
  console.error(err);
  const f = document.getElementById('fatal');
  if (f) {
    f.classList.remove('hidden');
    f.textContent = 'Failed to start: ' + String(err?.message ?? err) + ' — WebGL may be unavailable on this device.';
  }
});
