import { Music } from './Music';

export type Sfx = 'coin' | 'pickup' | 'deliver' | 'crash' | 'jump' | 'land' | 'lane' | 'brake' | 'horn' | 'click' | 'fuel' | 'lowFuel' | 'gameOver' | 'newBest' | 'miss' | 'full' | 'start';

/**
 * All sound is synthesised with WebAudio: engine rumble that follows speed,
 * the signature two-tone jeepney horn, coins, crashes and the music.
 * Music and SFX have independent toggles and gain stages.
 */
export class AudioManager {
  private ctx: AudioContext | null = null;
  private master!: GainNode;
  private sfxGain!: GainNode;
  private musicGain!: GainNode;
  private music: Music | null = null;
  private noise!: AudioBuffer;
  private engine: { o1: OscillatorNode; o2: OscillatorNode; lfo: OscillatorNode; filter: BiquadFilterNode; gain: GainNode } | null = null;
  private ambience: { src: AudioBufferSourceNode; gain: GainNode } | null = null;
  musicOn = true;
  sfxOn = true;
  private wantedMusic: 'run' | 'menu' | null = null;
  private lastCoin = 0;
  private coinPitch = 0;

  /** Must be called from a user gesture (browser autoplay policy). */
  unlock(): void {
    if (this.ctx) {
      if (this.ctx.state === 'suspended') this.ctx.resume().catch(() => {});
      return;
    }
    const AC: typeof AudioContext | undefined = window.AudioContext ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
    if (!AC) return;
    try {
      this.ctx = new AC({ latencyHint: 'interactive' });
    } catch (e) {
      console.warn('[audio] unavailable', e);
      return;
    }
    const ctx = this.ctx;
    this.master = ctx.createGain();
    this.master.gain.value = 0.9;
    const comp = ctx.createDynamicsCompressor();
    comp.threshold.value = -14;
    comp.ratio.value = 4;
    this.master.connect(comp).connect(ctx.destination);
    this.sfxGain = ctx.createGain();
    this.sfxGain.gain.value = this.sfxOn ? 1 : 0;
    this.sfxGain.connect(this.master);
    this.musicGain = ctx.createGain();
    this.musicGain.gain.value = this.musicOn ? 0.55 : 0;
    this.musicGain.connect(this.master);
    const len = ctx.sampleRate;
    this.noise = ctx.createBuffer(1, len, ctx.sampleRate);
    const d = this.noise.getChannelData(0);
    for (let i = 0; i < len; i++) d[i] = Math.random() * 2 - 1;
    this.music = new Music(ctx, this.musicGain);
    if (this.wantedMusic) this.playMusic(this.wantedMusic);
  }

  get ready(): boolean {
    return !!this.ctx;
  }

  setMusic(on: boolean): void {
    this.musicOn = on;
    if (this.ctx) this.musicGain.gain.setTargetAtTime(on ? 0.55 : 0, this.ctx.currentTime, 0.05);
  }

  setSfx(on: boolean): void {
    this.sfxOn = on;
    if (this.ctx) this.sfxGain.gain.setTargetAtTime(on ? 1 : 0, this.ctx.currentTime, 0.05);
  }

  playMusic(which: 'run' | 'menu'): void {
    this.wantedMusic = which;
    this.music?.play(which);
  }

  stopMusic(): void {
    this.wantedMusic = null;
    this.music?.stop();
  }

  /** App backgrounded / phone call: silence everything. */
  suspend(): void {
    this.ctx?.suspend().catch(() => {});
  }

  resume(): void {
    this.ctx?.resume().catch(() => {});
  }

  // ---------------------------------------------------------------- engine
  startEngine(): void {
    const ctx = this.ctx;
    if (!ctx || this.engine) return;
    const o1 = ctx.createOscillator();
    const o2 = ctx.createOscillator();
    const lfo = ctx.createOscillator();
    const lfoGain = ctx.createGain();
    const filter = ctx.createBiquadFilter();
    const gain = ctx.createGain();
    o1.type = 'sawtooth';
    o2.type = 'square';
    o1.frequency.value = 48;
    o2.frequency.value = 24.5;
    lfo.frequency.value = 11; // diesel chug
    lfoGain.gain.value = 0.03;
    lfo.connect(lfoGain).connect(gain.gain);
    filter.type = 'lowpass';
    filter.frequency.value = 380;
    filter.Q.value = 2;
    gain.gain.value = 0.0;
    gain.gain.setTargetAtTime(0.075, ctx.currentTime, 0.3);
    const g2 = ctx.createGain();
    g2.gain.value = 0.6;
    o1.connect(filter);
    o2.connect(g2).connect(filter);
    filter.connect(gain).connect(this.sfxGain);
    o1.start();
    o2.start();
    lfo.start();
    this.engine = { o1, o2, lfo, filter, gain };
    // street ambience: filtered noise bed
    const src = ctx.createBufferSource();
    src.buffer = this.noise;
    src.loop = true;
    const bf = ctx.createBiquadFilter();
    bf.type = 'bandpass';
    bf.frequency.value = 500;
    bf.Q.value = 0.4;
    const ag = ctx.createGain();
    ag.gain.value = 0.02;
    src.connect(bf).connect(ag).connect(this.sfxGain);
    src.start();
    this.ambience = { src, gain: ag };
  }

  updateEngine(speedFactor: number, gas: boolean): void {
    const e = this.engine;
    if (!e || !this.ctx) return;
    const t = this.ctx.currentTime;
    const f = 42 + speedFactor * 55 + (gas ? 18 : 0);
    e.o1.frequency.setTargetAtTime(f, t, 0.12);
    e.o2.frequency.setTargetAtTime(f * 0.5 + 0.7, t, 0.12);
    e.lfo.frequency.setTargetAtTime(9 + speedFactor * 14, t, 0.2);
    e.filter.frequency.setTargetAtTime(320 + speedFactor * 600 + (gas ? 300 : 0), t, 0.1);
    this.ambience?.gain.gain.setTargetAtTime(0.012 + speedFactor * 0.025, t, 0.3);
  }

  stopEngine(): void {
    if (!this.engine || !this.ctx) return;
    const { o1, o2, lfo, gain } = this.engine;
    const t = this.ctx.currentTime;
    gain.gain.setTargetAtTime(0, t, 0.08);
    [o1, o2, lfo].forEach((o) => o.stop(t + 0.5));
    this.engine = null;
    if (this.ambience) {
      this.ambience.gain.gain.setTargetAtTime(0, t, 0.1);
      this.ambience.src.stop(t + 0.5);
      this.ambience = null;
    }
  }

  // ------------------------------------------------------------- one-shots
  private tone(type: OscillatorType, f0: number, f1: number, t: number, dur: number, vol: number, attack = 0.005): void {
    const ctx = this.ctx!;
    const o = ctx.createOscillator();
    const g = ctx.createGain();
    o.type = type;
    o.frequency.setValueAtTime(f0, t);
    if (f1 !== f0) o.frequency.exponentialRampToValueAtTime(Math.max(1, f1), t + dur);
    g.gain.setValueAtTime(0.0001, t);
    g.gain.exponentialRampToValueAtTime(vol, t + attack);
    g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    o.connect(g).connect(this.sfxGain);
    o.start(t);
    o.stop(t + dur + 0.02);
  }

  private noiseBurst(t: number, dur: number, vol: number, type: BiquadFilterType, f0: number, f1 = f0, q = 1): void {
    const ctx = this.ctx!;
    const src = ctx.createBufferSource();
    src.buffer = this.noise;
    const f = ctx.createBiquadFilter();
    f.type = type;
    f.Q.value = q;
    f.frequency.setValueAtTime(f0, t);
    f.frequency.exponentialRampToValueAtTime(Math.max(20, f1), t + dur);
    const g = ctx.createGain();
    g.gain.setValueAtTime(vol, t);
    g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    src.connect(f).connect(g).connect(this.sfxGain);
    src.start(t, Math.random() * 0.5);
    src.stop(t + dur + 0.02);
  }

  /** The signature jeepney horn: brassy two-tone "beep-beeep". */
  private horn(t: number): void {
    const ctx = this.ctx!;
    const blast = (st: number, dur: number) => {
      const out = ctx.createGain();
      const shaper = ctx.createWaveShaper();
      const curve = new Float32Array(256);
      for (let i = 0; i < 256; i++) {
        const x = (i / 255) * 2 - 1;
        curve[i] = Math.tanh(x * 3);
      }
      shaper.curve = curve;
      const bp = ctx.createBiquadFilter();
      bp.type = 'bandpass';
      bp.frequency.value = 900;
      bp.Q.value = 0.7;
      out.gain.setValueAtTime(0.0001, st);
      out.gain.exponentialRampToValueAtTime(0.22, st + 0.015);
      out.gain.setValueAtTime(0.22, st + dur - 0.04);
      out.gain.exponentialRampToValueAtTime(0.0001, st + dur);
      for (const f of [392, 494]) {
        const o = ctx.createOscillator();
        o.type = 'sawtooth';
        o.frequency.setValueAtTime(f * 0.97, st);
        o.frequency.linearRampToValueAtTime(f, st + 0.03);
        o.connect(shaper);
        o.start(st);
        o.stop(st + dur + 0.02);
      }
      shaper.connect(bp).connect(out).connect(this.sfxGain);
    };
    blast(t, 0.14);
    blast(t + 0.2, 0.38);
  }

  play(s: Sfx): void {
    if (!this.ctx || !this.sfxOn) return;
    const t = this.ctx.currentTime;
    switch (s) {
      case 'coin': {
        // rising pitch for quick coin chains
        this.coinPitch = t - this.lastCoin < 0.35 ? Math.min(this.coinPitch + 1, 8) : 0;
        this.lastCoin = t;
        const p = Math.pow(2, this.coinPitch / 12);
        this.tone('square', 988 * p, 988 * p, t, 0.06, 0.06);
        this.tone('square', 1319 * p, 1319 * p, t + 0.06, 0.14, 0.06);
        break;
      }
      case 'pickup':
        [523, 659, 784, 1047].forEach((f, i) => this.tone('triangle', f, f, t + i * 0.06, 0.18, 0.18));
        break;
      case 'deliver':
        this.noiseBurst(t, 0.08, 0.15, 'highpass', 4000);
        [1568, 2093, 2637].forEach((f, i) => this.tone('sine', f, f, t + 0.05 + i * 0.07, 0.5, 0.16));
        this.tone('triangle', 523, 523, t, 0.25, 0.12);
        break;
      case 'crash':
        this.noiseBurst(t, 0.5, 0.6, 'lowpass', 3000, 200);
        this.tone('sine', 140, 35, t, 0.4, 0.6);
        this.noiseBurst(t + 0.05, 0.3, 0.25, 'bandpass', 5000, 2500, 3); // glass
        break;
      case 'jump':
        this.noiseBurst(t, 0.25, 0.12, 'bandpass', 400, 1800, 2);
        this.tone('sine', 220, 520, t, 0.18, 0.12);
        break;
      case 'land':
        this.tone('sine', 110, 50, t, 0.15, 0.3);
        this.noiseBurst(t, 0.12, 0.1, 'lowpass', 800, 200);
        break;
      case 'lane':
        this.noiseBurst(t, 0.16, 0.08, 'bandpass', 900, 2200, 1.5);
        break;
      case 'brake':
        this.tone('sawtooth', 1150, 900, t, 0.45, 0.035);
        this.noiseBurst(t, 0.45, 0.08, 'bandpass', 2500, 1800, 6);
        break;
      case 'horn':
        this.horn(t);
        break;
      case 'click':
        this.tone('square', 880, 660, t, 0.05, 0.05);
        break;
      case 'fuel':
        [300, 380, 460, 560].forEach((f, i) => this.tone('sine', f, f * 1.2, t + i * 0.05, 0.1, 0.16));
        break;
      case 'lowFuel':
        this.tone('square', 660, 660, t, 0.1, 0.06);
        this.tone('square', 660, 660, t + 0.18, 0.1, 0.06);
        break;
      case 'miss':
        this.tone('triangle', 392, 330, t, 0.18, 0.15);
        this.tone('triangle', 330, 262, t + 0.16, 0.25, 0.15);
        break;
      case 'full':
        this.tone('square', 440, 440, t, 0.08, 0.05);
        this.tone('square', 349, 349, t + 0.1, 0.12, 0.05);
        break;
      case 'gameOver':
        [523, 466, 392, 311].forEach((f, i) => this.tone('triangle', f, f, t + i * 0.16, 0.3, 0.18));
        break;
      case 'newBest':
        [523, 659, 784, 1047, 784, 1047, 1319].forEach((f, i) => this.tone('square', f, f, t + i * 0.09, 0.2, 0.07));
        break;
      case 'start':
        this.horn(t);
        break;
    }
  }
}
