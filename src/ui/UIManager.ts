import type { GameManager, GameUI, RunResult } from '../core/GameManager';
import type { AudioManager } from '../audio/AudioManager';
import type { SaveManager } from '../save/SaveManager';
import type { Renderer, Quality } from '../render/Renderer';
import { FUEL } from '../config';
import { setVibration, haptic } from '../platform/Platform';

const $ = <T extends HTMLElement = HTMLElement>(id: string): T => {
  const el = document.getElementById(id);
  if (!el) throw new Error(`#${id} missing`);
  return el as T;
};

type ScreenId = 'splash' | 'menu' | 'hud' | 'pause' | 'gameover' | 'settings' | 'howto' | 'stats' | 'confirm';

/**
 * DOM-based UI layered over the WebGL canvas. All screens are real and
 * functional; HUD writes only touch the DOM when a value changes.
 */
export class UIManager implements GameUI {
  private last: Record<string, string | number | boolean> = {};
  private modalStack: ScreenId[] = [];
  private confirmCb: (() => void) | null = null;
  private floatCount = 0;

  constructor(private audio: AudioManager, private save: SaveManager, private renderer: Renderer) {}

  // ------------------------------------------------------------ plumbing
  private show(id: ScreenId, on = true): void {
    $(id).classList.toggle('active', on);
  }

  private openModal(id: ScreenId): void {
    this.show(id);
    this.modalStack.push(id);
  }

  private closeModal(): ScreenId | undefined {
    const id = this.modalStack.pop();
    if (id) this.show(id, false);
    return id;
  }

  /** Back button: close the top modal if any. Returns true if handled. */
  back(): boolean {
    if (this.modalStack.length) {
      this.click();
      this.closeModal();
      return true;
    }
    return false;
  }

  private click(): void {
    this.audio.play('click');
    haptic('light');
  }

  private bind(id: string, fn: () => void): void {
    $(id).addEventListener('click', (e) => {
      e.preventDefault();
      this.click();
      fn();
    });
  }

  setLoading(p: number, text: string): void {
    $('loadFill').style.width = `${Math.round(p * 100)}%`;
    $('loadText').textContent = text;
  }

  // -------------------------------------------------------------- wiring
  attach(game: GameManager, onTapStart: () => void): void {
    $('ver').textContent = __APP_VERSION__;
    $('btnTapStart').addEventListener('click', () => {
      onTapStart();
    });
    this.bind('btnPlay', () => {
      this.show('menu', false);
      const tut = $('tutorial');
      if (!this.save.data.settings.tutorialSeen) {
        tut.classList.remove('hidden');
        this.save.data.settings.tutorialSeen = true;
        this.save.save();
        setTimeout(() => tut.classList.add('hidden'), 7500);
      } else tut.classList.add('hidden');
      game.startRun();
    });
    this.bind('btnHow', () => this.openModal('howto'));
    this.bind('btnHowBack', () => this.closeModal());
    this.bind('btnStats', () => {
      this.renderStats();
      this.openModal('stats');
    });
    this.bind('btnStatsBack', () => this.closeModal());
    this.bind('btnSettings', () => {
      this.syncSettings();
      this.openModal('settings');
    });
    this.bind('btnSettingsBack', () => this.closeModal());
    this.bind('btnHonkMenu', () => this.audio.play('horn'));
    $('btnHorn').addEventListener('pointerdown', (e) => {
      e.preventDefault();
      game.honk();
      haptic('light');
    });
    this.bind('btnPause', () => game.pause());
    this.bind('btnResume', () => game.resume());
    this.bind('btnRestart', () => game.startRun());
    this.bind('btnPauseMenu', () => game.toMenu());
    this.bind('btnRetry', () => {
      this.show('gameover', false);
      game.startRun();
    });
    this.bind('btnHome', () => {
      this.show('gameover', false);
      game.toMenu();
    });
    this.bind('btnReset', () =>
      this.confirm('Reset progress?', 'This erases coins, best scores and records on this device.', () => {
        this.save.resetProgress();
        this.refreshMenu();
      }),
    );
    this.bind('btnConfirmYes', () => {
      this.closeModal();
      const cb = this.confirmCb;
      this.confirmCb = null;
      cb?.();
    });
    this.bind('btnConfirmNo', () => this.closeModal());

    const s = this.save.data.settings;
    const music = (on: boolean) => {
      s.music = on;
      this.audio.setMusic(on);
      this.save.save();
      this.syncSettings();
    };
    const sfx = (on: boolean) => {
      s.sfx = on;
      this.audio.setSfx(on);
      this.save.save();
      this.syncSettings();
    };
    $<HTMLInputElement>('sMusic').addEventListener('change', (e) => music((e.target as HTMLInputElement).checked));
    $<HTMLInputElement>('pMusic').addEventListener('change', (e) => music((e.target as HTMLInputElement).checked));
    $<HTMLInputElement>('sSfx').addEventListener('change', (e) => sfx((e.target as HTMLInputElement).checked));
    $<HTMLInputElement>('pSfx').addEventListener('change', (e) => sfx((e.target as HTMLInputElement).checked));
    $<HTMLInputElement>('sVib').addEventListener('change', (e) => {
      s.vibration = (e.target as HTMLInputElement).checked;
      setVibration(s.vibration);
      this.save.save();
    });
    $<HTMLInputElement>('sFps').addEventListener('change', (e) => {
      s.showFps = (e.target as HTMLInputElement).checked;
      $('fps').classList.toggle('hidden', !s.showFps);
      this.save.save();
    });
    $('sQuality').querySelectorAll<HTMLButtonElement>('button').forEach((b) =>
      b.addEventListener('click', () => {
        this.click();
        s.quality = b.dataset.q as Quality;
        this.renderer.setQuality(s.quality);
        this.save.save();
        this.syncSettings();
      }),
    );
    $('fps').classList.toggle('hidden', !s.showFps);
    setVibration(s.vibration);
  }

  showTapToStart(): void {
    $('btnTapStart').classList.remove('hidden');
    document.querySelector('.loader')?.classList.add('hidden');
  }

  private confirm(title: string, text: string, cb: () => void): void {
    $('confirmTitle').textContent = title;
    $('confirmText').textContent = text;
    this.confirmCb = cb;
    this.openModal('confirm');
  }

  private syncSettings(): void {
    const s = this.save.data.settings;
    for (const id of ['sMusic', 'pMusic']) $<HTMLInputElement>(id).checked = s.music;
    for (const id of ['sSfx', 'pSfx']) $<HTMLInputElement>(id).checked = s.sfx;
    $<HTMLInputElement>('sVib').checked = s.vibration;
    $<HTMLInputElement>('sFps').checked = s.showFps;
    $('sQuality').querySelectorAll<HTMLButtonElement>('button').forEach((b) => b.classList.toggle('on', b.dataset.q === s.quality));
  }

  private renderStats(): void {
    const d = this.save.data;
    const st = d.stats;
    const cells: [string, string][] = [
      ['Best score', d.bestScore.toLocaleString()],
      ['Best distance', `${Math.round(d.bestDistance).toLocaleString()} m`],
      ['Runs', st.runs.toLocaleString()],
      ['Total distance', `${(st.totalDistance / 1000).toFixed(1)} km`],
      ['Passengers delivered', st.totalDelivered.toLocaleString()],
      ['Coins earned', st.totalCoins.toLocaleString()],
      ['Crashes', st.crashes.toLocaleString()],
      ['Time on the road', `${Math.floor(st.playTimeSec / 60)} min`],
    ];
    $('statsGrid').innerHTML = cells.map(([k, v]) => `<div><span class="k">${k}</span><span class="v">${v}</span></div>`).join('');
    const list = $('topList');
    list.innerHTML = d.topRuns.length
      ? d.topRuns.map((r) => `<li><span class="s">${r.score.toLocaleString()}</span> <span class="d">· ${r.distance.toLocaleString()} m · ${r.delivered} delivered · ${new Date(r.at).toLocaleDateString()}</span></li>`).join('')
      : '<li class="d">No runs yet — hit PLAY!</li>';
  }

  refreshMenu(): void {
    $('menuCoins').textContent = this.save.data.coins.toLocaleString();
    $('menuBest').textContent = this.save.data.bestScore.toLocaleString();
  }

  // ------------------------------------------------------------ GameUI
  showMenu(): void {
    this.show('splash', false);
    for (const id of ['hud', 'pause', 'gameover'] as ScreenId[]) this.show(id, false);
    this.refreshMenu();
    this.show('menu');
  }

  showHud(on: boolean): void {
    this.show('hud', on);
    if (on) {
      this.show('menu', false);
      this.last = {};
      $('floats').innerHTML = '';
    }
  }

  showPause(on: boolean): void {
    if (on) this.syncSettings();
    this.show('pause', on);
  }

  showGameOver(r: RunResult): void {
    $('goTitle').textContent = r.reason === 'fuel' ? 'UBOS GAS!' : 'BANGGA!';
    $('goBest').classList.toggle('hidden', !r.newBest);
    $('goScore').textContent = r.score.toLocaleString();
    $('goDist').textContent = `${Math.round(r.distance).toLocaleString()} m`;
    $('goCoins').textContent = r.coins.toLocaleString();
    $('goDelivered').textContent = String(r.delivered);
    $('goPicked').textContent = String(r.pickedUp);
    $('goStreak').textContent = `x${r.bestStreak}`;
    $('goBestScore').textContent = r.bestScore.toLocaleString();
    $('goReward').textContent = r.coins.toLocaleString();
    $('goWallet').textContent = this.save.data.coins.toLocaleString();
    this.show('gameover');
  }

  private set(id: string, key: string, v: string | number): void {
    if (this.last[key] === v) return;
    this.last[key] = v;
    $(id).textContent = String(v);
  }

  updateHud(g: GameManager): void {
    const r = g.run;
    this.set('hudScore', 'score', Math.floor(r.score).toLocaleString());
    this.set('hudDist', 'dist', `${Math.floor(r.distance).toLocaleString()} m`);
    this.set('hudCoins', 'coins', r.coins.toLocaleString());
    this.set('hudSpeed', 'speed', Math.round(r.speed * 3.6));
    const route = g.world.route;
    this.set('hudPax', 'pax', `${route.count}/${route.capacity}`);
    if (this.last.full !== route.count >= route.capacity) {
      this.last.full = route.count >= route.capacity;
      $('hudPax').parentElement!.classList.toggle('full', route.count >= route.capacity);
    }
    // hearts
    const hp = `${g.player.health}/${g.player.maxHealth}`;
    if (this.last.hp !== hp) {
      const prev = this.last.hp as string | undefined;
      this.last.hp = hp;
      const el = $('hudHearts');
      el.innerHTML = '';
      for (let i = 0; i < g.player.maxHealth; i++) {
        const s = document.createElement('span');
        s.className = 'heart' + (i < g.player.health ? '' : ' off') + (prev && i === g.player.health ? ' pop' : '');
        s.textContent = '❤️';
        el.appendChild(s);
      }
    }
    // fuel
    const fuelPct = Math.round((r.fuel / FUEL.max) * 100);
    if (this.last.fuel !== fuelPct) {
      this.last.fuel = fuelPct;
      $('hudFuel').style.width = `${fuelPct}%`;
      $('hudFuel').parentElement!.parentElement!.classList.toggle('low', r.fuel < FUEL.lowThreshold);
    }
    if (this.last.gas !== r.gas) {
      this.last.gas = r.gas;
      $('gasTag').classList.toggle('on', r.gas);
    }
    // next stop
    const ns = g.nextStop();
    const key = ns ? `${ns.name}|${Math.ceil(ns.dist / 10)}|${ns.alighting}|${ns.side}` : 'none';
    if (this.last.stop !== key) {
      this.last.stop = key;
      if (ns) {
        $('stopName').textContent = ns.name;
        $('stopArrowL').classList.toggle('on', ns.side < 0 && ns.dist < 160);
        $('stopArrowR').classList.toggle('on', ns.side > 0 && ns.dist < 160);
        const lane = ns.side < 0 ? 'left lane' : 'right lane';
        $('stopMeta').innerHTML = `${Math.ceil(ns.dist)} m · ${lane}` + (ns.alighting ? ` · <span class="drop">${ns.alighting} bababa ⬇</span>` : '');
        $('stopPanel').classList.toggle('dropping', ns.alighting > 0);
      }
    }
    if (g.save.data.settings.showFps) this.set('fps', 'fps', `${g.fps} fps · ${g.world.allHazards().length} obj`);
  }

  float(text: string, kind: 'coin' | 'good' | 'bad' | 'info' | 'big'): void {
    const host = $('floats');
    if (host.childElementCount > 4) host.firstElementChild?.remove();
    const el = document.createElement('div');
    el.className = `float ${kind}`;
    el.textContent = text;
    el.dataset.n = String(this.floatCount++);
    host.appendChild(el);
    setTimeout(() => el.remove(), 1300);
  }

  flash(kind: 'hit' | 'good'): void {
    const f = $('flash');
    f.className = '';
    void f.offsetWidth; // restart animation
    f.className = kind;
  }

  fatal(msg: string): void {
    const f = $('fatal');
    f.classList.remove('hidden');
    f.innerHTML = `<div style="font-family:var(--display);font-size:32px;color:#fcd116">Naku! Something went wrong</div><div></div><button class="btn btn-play" style="max-width:280px" onclick="location.reload()">RELOAD</button>`;
    (f.children[1] as HTMLElement).textContent = msg;
  }
}

