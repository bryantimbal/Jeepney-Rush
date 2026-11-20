import { FUEL, LANES, PLAYER, REWARD, SCORE, SPEED, baseSpeedAt } from '../config';
import type { Renderer } from '../render/Renderer';
import type { World } from '../game/World';
import type { PlayerController } from '../game/PlayerController';
import type { AudioManager } from '../audio/AudioManager';
import type { SaveManager } from '../save/SaveManager';
import type { Input, Action } from './Input';
import { Emitter, type GameEvents } from './Events';
import { overlapsAt } from '../game/logic/Collision';
import { haptic } from '../platform/Platform';
import type { Stop } from '../game/logic/PassengerRoute';

export type GameState = 'menu' | 'playing' | 'paused' | 'dying' | 'gameover';

export interface RunState {
  distance: number;
  score: number;
  coins: number;
  speed: number;
  fuel: number;
  time: number;
  gas: boolean;
  crashSlow: number;
  outOfFuel: boolean;
  lowFuelWarned: boolean;
  hits: number;
  bestStreak: number;
  startT: number;
  endReason: 'crash' | 'fuel' | null;
}

export interface RunResult {
  score: number;
  distance: number;
  coins: number;
  delivered: number;
  pickedUp: number;
  missed: number;
  bestStreak: number;
  newBest: boolean;
  bestScore: number;
  reason: 'crash' | 'fuel';
  seconds: number;
}

/** What the GameManager needs from the UI layer. */
export interface GameUI {
  showHud(on: boolean): void;
  updateHud(g: GameManager): void;
  float(text: string, kind: 'coin' | 'good' | 'bad' | 'info' | 'big'): void;
  flash(kind: 'hit' | 'good'): void;
  showPause(on: boolean): void;
  showGameOver(r: RunResult): void;
  showMenu(): void;
}

function newRun(): RunState {
  return { distance: 0, score: 0, coins: 0, speed: 0, fuel: FUEL.max, time: 0, gas: false, crashSlow: 1, outOfFuel: false, lowFuelWarned: false, hits: 0, bestStreak: 0, startT: 0, endReason: null };
}

/**
 * Owns the game state machine and the per-frame simulation order:
 * input → player → speed → spawning → world → collisions → pickups → HUD.
 */
export class GameManager {
  state: GameState = 'menu';
  run: RunState = newRun();
  readonly events = new Emitter<GameEvents>();
  private menuT = 0;
  private camBlend = 1;
  private dyingT = 0;
  private exhaustT = 0;
  private lastFrame = performance.now();
  private fpsAcc = 0;
  private fpsFrames = 0;
  fps = 60;
  ui!: GameUI;
  /** Seed override for automated tests (?seed=123). */
  seed: number | null = null;

  constructor(
    readonly renderer: Renderer,
    readonly world: World,
    readonly player: PlayerController,
    readonly audio: AudioManager,
    readonly save: SaveManager,
    readonly input: Input,
  ) {
    input.onAction((a) => this.onAction(a));
    this.wireEvents();
  }

  private wireEvents(): void {
    const e = this.events;
    const a = this.audio;
    e.on('coin', () => a.play('coin'));
    e.on('fuel', () => a.play('fuel'));
    e.on('jump', () => a.play('jump'));
    e.on('land', () => a.play('land'));
    e.on('lane', () => a.play('lane'));
    e.on('brake', () => a.play('brake'));
    e.on('honk', () => a.play('horn'));
    e.on('lowFuel', () => a.play('lowFuel'));
    e.on('hit', () => {
      a.play('crash');
      haptic('heavy');
    });
    e.on('pickup', () => {
      a.play('pickup');
      haptic('light');
    });
    e.on('deliver', () => {
      a.play('deliver');
      haptic('medium');
    });
    e.on('missed', () => a.play('miss'));
    e.on('full', () => a.play('full'));
  }

  // ------------------------------------------------------------------ flow
  startRun(): void {
    const seed = this.seed ?? (Date.now() & 0x7fffffff);
    this.world.reset(seed, PLAYER.capacity);
    this.player.reset();
    this.run = newRun();
    this.run.startT = performance.now();
    this.state = 'playing';
    this.camBlend = 0;
    this.input.enabled = true;
    this.input.reset();
    this.audio.startEngine();
    this.audio.playMusic('run');
    this.audio.play('start');
    this.ui.showPause(false);
    this.ui.showHud(true);
    this.ui.float('ARANGKADA!', 'big');
  }

  pause(): void {
    if (this.state !== 'playing') return;
    this.state = 'paused';
    this.input.enabled = false;
    this.input.reset();
    this.audio.stopEngine();
    this.ui.showPause(true);
  }

  resume(): void {
    if (this.state !== 'paused') return;
    this.state = 'playing';
    this.input.enabled = true;
    this.audio.startEngine();
    this.ui.showPause(false);
    this.lastFrame = performance.now();
  }

  toMenu(): void {
    this.state = 'menu';
    this.input.enabled = false;
    this.audio.stopEngine();
    this.audio.playMusic('menu');
    this.world.reset(Date.now() & 0xffff, PLAYER.capacity);
    this.player.reset();
    this.ui.showPause(false);
    this.ui.showHud(false);
    this.ui.showMenu();
  }

  /** Android back button / Escape. Returns true if handled. */
  back(): boolean {
    if (this.state === 'playing') {
      this.pause();
      return true;
    }
    if (this.state === 'paused') {
      this.resume();
      return true;
    }
    return false;
  }

  private onAction(a: Action): void {
    if (a === 'pause') {
      if (this.state === 'playing') this.pause();
      else if (this.state === 'paused') this.resume();
      return;
    }
    if (this.state !== 'playing' || this.run.outOfFuel) return;
    const p = this.player;
    switch (a) {
      case 'left':
      case 'right':
        if (p.move(a === 'left' ? -1 : 1)) this.events.emit('lane', { dir: a === 'left' ? -1 : 1 });
        break;
      case 'up':
        if (p.jump()) this.events.emit('jump', {});
        break;
      case 'down':
        if (p.brake() && p.grounded) this.events.emit('brake', {});
        break;
      case 'horn':
        this.honk();
        break;
    }
  }

  honk(): void {
    if (this.state !== 'playing' && this.state !== 'menu') return;
    this.events.emit('honk', {});
  }

  private endRun(reason: 'crash' | 'fuel'): void {
    if (this.state !== 'playing') return;
    this.state = 'dying';
    this.run.endReason = reason;
    this.dyingT = 0;
    this.input.enabled = false;
    this.audio.stopEngine();
  }

  private finishRun(): void {
    const r = this.run;
    const route = this.world.route;
    const seconds = (performance.now() - r.startT) / 1000;
    const rec = this.save.recordRun({ score: r.score, distance: r.distance, coins: r.coins, delivered: route.delivered, pickedUp: route.pickedUp, crashed: r.endReason === 'crash', seconds });
    this.state = 'gameover';
    this.ui.showHud(false);
    this.audio.play(rec.newBest ? 'newBest' : 'gameOver');
    this.ui.showGameOver({
      score: Math.floor(r.score),
      distance: r.distance,
      coins: r.coins,
      delivered: route.delivered,
      pickedUp: route.pickedUp,
      missed: route.missed,
      bestStreak: r.bestStreak,
      newBest: rec.newBest,
      bestScore: this.save.data.bestScore,
      reason: r.endReason ?? 'crash',
      seconds,
    });
  }

  // ------------------------------------------------------------- main loop
  frame(now: number): void {
    let dt = (now - this.lastFrame) / 1000;
    this.lastFrame = now;
    if (!(dt > 0)) dt = 1 / 60;
    dt = Math.min(dt, 1 / 20); // avoid tunnelling after hitches
    this.fpsAcc += dt;
    this.fpsFrames++;
    if (this.fpsAcc > 0.5) {
      this.fps = Math.round(this.fpsFrames / this.fpsAcc);
      this.fpsAcc = 0;
      this.fpsFrames = 0;
    }

    switch (this.state) {
      case 'menu':
      case 'gameover':
        this.updateMenu(dt);
        break;
      case 'playing':
        this.updateRun(dt);
        break;
      case 'dying':
        this.dyingT += dt;
        this.updateRun(dt * 0.3, true);
        if (this.dyingT > 1.1) this.finishRun();
        break;
      case 'paused':
        break;
    }
    this.renderer.render();
  }

  private updateMenu(dt: number): void {
    this.menuT += dt;
    const cruise = this.state === 'menu' ? 7 : 0;
    this.world.idle(dt, cruise);
    this.player.update(dt, cruise, false);
    this.world.effects.update(dt, cruise, 0);
    // slow orbit showcasing the jeepney's front: horses, chrome and lights
    const a = Math.sin(this.menuT * 0.25) * 0.75 + 0.35;
    const cam = this.renderer.camera;
    const r = 8.6;
    cam.position.set(Math.sin(a) * r - 1.2, 2.7 + Math.sin(this.menuT * 0.4) * 0.3, -Math.cos(a) * r - 0.3);
    cam.lookAt(0, 1.3, -0.4);
    if (this.state === 'menu' && Math.random() < dt * 2.5) this.world.effects.exhaust(this.player.x + 0.62, 0.55, 3.1, false);
    this.renderer.setRoadBend(0);
  }

  private updateRun(dt: number, dying = false): void {
    const r = this.run;
    const p = this.player;
    const w = this.world;
    r.time += dt;

    // ---- speed model
    r.gas = !dying && !r.outOfFuel && this.input.holding;
    const base = baseSpeedAt(r.distance);
    let target = base * (r.gas ? SPEED.gasMultiplier : 1);
    if (p.brakeT > 0) target *= SPEED.brakeMultiplier;
    r.crashSlow = Math.min(1, r.crashSlow + dt * 0.6);
    target *= r.crashSlow;
    if (r.outOfFuel || dying) target = 0;
    const intro = Math.min(1, r.time / 1.2);
    const accel = p.brakeT > 0 || r.outOfFuel ? 5 : r.time < 1.2 ? 2.5 : 1.8;
    r.speed += (target * (0.35 + 0.65 * intro) - r.speed) * Math.min(1, dt * accel);
    const speed = Math.max(0, r.speed);

    // ---- player
    const ev = p.update(dt, speed, r.gas);
    if (ev === 'land') {
      this.events.emit('land', {});
      w.effects.dust(p.x, 1.5);
      this.renderer.addShake(0.06);
    }

    // ---- distance, score, fuel
    const dz = speed * dt;
    r.distance += dz;
    if (!dying) {
      r.score += dz * SCORE.perMetre * (r.gas ? SCORE.gasMultiplier : 1);
      r.fuel -= FUEL.drainPerSecond * dt * (r.gas ? FUEL.gasDrainMultiplier : 1);
      if (r.fuel < FUEL.lowThreshold && !r.lowFuelWarned) {
        r.lowFuelWarned = true;
        this.events.emit('lowFuel', {});
        this.ui.float('MAUUBUSAN NG GAS!', 'bad');
      }
      if (r.fuel > FUEL.lowThreshold + 5) r.lowFuelWarned = false;
      if (r.fuel <= 0 && !r.outOfFuel) {
        r.fuel = 0;
        r.outOfFuel = true;
        this.ui.float('UBOS NA ANG GAS!', 'bad');
      }
      if (r.outOfFuel && speed < 0.8) this.endRun('fuel');
    }

    // ---- world
    if (!dying) {
      w.currentLanes = p.occupiedLanes();
      w.director.update(r.distance, Math.max(speed, base * 0.6));
    }
    w.road.update(dt, speed);
    w.traffic.update(dt, speed);
    w.obstacles.update(dt, speed);
    w.effects.update(dt, speed, Math.max(0, (speed - SPEED.start) / (SPEED.max - SPEED.start)) + (r.gas ? 0.35 : 0));
    this.exhaustT -= dt;
    if (this.exhaustT <= 0) {
      this.exhaustT = r.gas ? 0.05 : 0.14;
      w.effects.exhaust(p.x + 0.62, p.y + 0.55, 3.1, r.gas);
    }

    if (!dying) {
      this.checkCollisions();
      w.coins.update(dt, speed, p.x, p.y, (c) => this.onCoin(c), () => this.onFuel());
      w.passengers.update(dt, speed, p.x, (res, stop) => this.onServe(res, stop), (lost, stop) => this.onMiss(lost, stop));
    } else {
      w.coins.update(dt, speed, 999, 0, () => {}, () => {});
    }

    // ---- camera & audio
    const sf = Math.max(0, (speed - SPEED.start) / (SPEED.max - SPEED.start));
    if (this.camBlend < 1) {
      this.camBlend = Math.min(1, this.camBlend + dt * 1.4);
      const cam = this.renderer.camera;
      const from = cam.position.clone();
      this.renderer.updateCamera(dt, p.x, p.y, sf + (r.gas ? 0.4 : 0));
      const k = this.camBlend * this.camBlend * (3 - 2 * this.camBlend);
      cam.position.lerpVectors(from, cam.position, Math.max(k, 0.08));
      cam.lookAt(this.renderer.camTarget);
    } else {
      this.renderer.updateCamera(dt, p.x, p.y, sf + (r.gas ? 0.4 : 0));
    }
    this.renderer.setRoadBend(Math.sin(r.distance * 0.0035) * 0.0009 + Math.sin(r.distance * 0.0011) * 0.0006);
    this.audio.updateEngine(sf, r.gas);
    if (!dying) this.ui.updateHud(this);
  }

  private checkCollisions(): void {
    const p = this.player;
    const r = this.run;
    for (const h of this.world.allHazards()) {
      if (h.hit) continue;
      if (!overlapsAt(p.x, p.y, h.x, h.z, h.def)) continue;
      if (p.invuln > 0) continue;
      const sideSwipe = p.changingLane && Math.abs(h.z) < h.def.halfL + PLAYER.halfLength - 1.4;
      p.damage();
      r.hits++;
      if (sideSwipe) p.bounceBack();
      r.crashSlow = h.def.jumpable ? 0.75 : SPEED.crashSlowdown;
      (h.speed > 0 ? this.world.traffic : this.world.obstacles).knock(h, p.x);
      this.world.effects.sparks((p.x + h.x) / 2, 1.0, -PLAYER.halfLength + 0.3);
      this.renderer.addShake(h.def.jumpable ? 0.25 : 0.55);
      this.ui.flash('hit');
      this.events.emit('hit', { health: p.health, kind: h.kind });
      if (p.health > 0) this.ui.float(h.def.jumpable ? 'ARAY!' : 'BANGGA!', 'bad');
      if (p.health <= 0) {
        this.endRun('crash');
        return;
      }
    }
  }

  private onCoin(c: { x: number; y: number; z: number }): void {
    this.run.coins += REWARD.coinValue;
    this.run.score += SCORE.coin;
    this.world.effects.coinSparkle(c.x, c.y, c.z);
    this.events.emit('coin', { value: REWARD.coinValue, ...c });
  }

  private onFuel(): void {
    this.run.fuel = Math.min(FUEL.max, this.run.fuel + FUEL.canAmount);
    this.run.outOfFuel = false;
    this.world.effects.confetti(this.player.x, 1.5, 0, 12);
    this.ui.float('+GAS', 'good');
    this.events.emit('fuel', { amount: FUEL.canAmount });
  }

  private onServe(res: { alighted: number; boarded: number; leftBehind: number; coins: number; score: number; streak: number }, stop: Stop): void {
    const r = this.run;
    r.coins += res.coins;
    r.score += res.score;
    r.bestStreak = Math.max(r.bestStreak, res.streak);
    if (res.alighted > 0) {
      this.ui.float(`+${res.alighted} BABA • +${res.coins} 🪙`, 'good');
      if (res.streak > 1) this.ui.float(`SUNOD-SUNOD x${res.streak}!`, 'info');
      this.world.effects.confetti(this.player.x, 2.5, 1.5, 30);
      this.ui.flash('good');
      this.events.emit('deliver', { count: res.alighted, coins: res.coins, stopName: stop.name, streak: res.streak });
    }
    if (res.boarded > 0) {
      if (res.alighted === 0) this.ui.float(`+${res.boarded} SAKAY!`, 'good');
      this.events.emit('pickup', { count: res.boarded, stopName: stop.name });
    }
    if (res.leftBehind > 0) {
      this.ui.float('PUNO NA!', 'info');
      this.events.emit('full', { stopName: stop.name });
    }
  }

  private onMiss(lost: number, stop: Stop): void {
    if (lost <= 0) return;
    this.ui.float(`LAGPAS! −${lost} pasahero`, 'bad');
    this.events.emit('missed', { count: lost, stopName: stop.name });
  }

  /** Next stop info for the HUD. */
  nextStop(): { name: string; dist: number; side: -1 | 1; alighting: number } | null {
    const stops = this.world.route.upcoming().filter((s) => s.at > this.run.distance - 10).sort((a, b) => a.at - b.at);
    if (!stops.length) return null;
    // prefer the next stop where passengers need to get off, if it is reasonably close
    const s = stops[0];
    return { name: s.name, dist: Math.max(0, s.at - this.run.distance), side: s.side, alighting: this.world.route.alightingAt(s.id) };
  }

  debugState(): Record<string, unknown> {
    return {
      state: this.state,
      distance: this.run.distance,
      score: this.run.score,
      coins: this.run.coins,
      speed: this.run.speed,
      fuel: this.run.fuel,
      health: this.player.health,
      lane: this.player.lane,
      passengers: this.world.route.count,
      delivered: this.world.route.delivered,
      pickedUp: this.world.route.pickedUp,
      hazards: this.world.allHazards().length,
      coinsActive: this.world.coins.activeCoins,
      fps: this.fps,
      lanes: LANES,
    };
  }
}

