/**
 * Original Filipino-inspired instrumental music, synthesised in real time.
 * Rondalla-style tremolo plucks (bandurria / octavina feel) over a bass line
 * and light percussion. No audio files needed.
 */
type Note = [step: number, midi: number | null, len: number];

interface Track {
  bpm: number;
  steps: number; // 16th-note steps per loop
  melody: Note[];
  bass: Note[];
  chords: [step: number, notes: number[]][];
  drums: boolean;
}

// MIDI helpers
const G4 = 67, A4 = 69, B4 = 71, C5 = 72, D5 = 74, E5 = 76, Fs5 = 78, G5 = 79, A5 = 81;
const D4 = 62, E4 = 64, Fs4 = 66;
const G2 = 43, C3 = 48, D3 = 50, E3 = 52, A2 = 45, B2 = 47;

/** Upbeat "Biyahe" theme – original melody in G major, 4 bars x 2. */
const RUN: Track = {
  bpm: 132,
  steps: 128,
  melody: [
    [0, D5, 2], [2, G5, 2], [4, Fs5, 2], [6, E5, 2], [8, D5, 4], [12, B4, 2], [14, D5, 2],
    [16, C5, 2], [18, E5, 2], [20, D5, 2], [22, C5, 2], [24, B4, 6], [30, A4, 2],
    [32, B4, 2], [34, D5, 2], [36, G5, 3], [39, Fs5, 1], [40, E5, 2], [42, D5, 2], [44, C5, 2], [46, B4, 2],
    [48, A4, 4], [52, D5, 2], [54, C5, 2], [56, B4, 2], [58, A4, 2], [60, G4, 4],
    [64, B4, 2], [66, C5, 2], [68, D5, 4], [72, E5, 2], [74, D5, 2], [76, C5, 2], [78, B4, 2],
    [80, A4, 2], [82, B4, 2], [84, C5, 4], [88, E5, 2], [90, D5, 2], [92, C5, 2], [94, A4, 2],
    [96, G5, 2], [98, Fs5, 2], [100, E5, 2], [102, D5, 2], [104, E5, 2], [106, Fs5, 2], [108, G5, 4],
    [112, A5, 2], [114, G5, 2], [116, Fs5, 2], [118, D5, 2], [120, G5, 6], [126, null, 2],
  ],
  bass: [],
  chords: [],
  drums: true,
};
// chord progression G – C – D – G | Em – C – D – G (two bars each half)
const PROG: [number, number[]][] = [
  [G2, [G4, B4, D5]], [C3, [G4, C5, E5]], [D3, [Fs4, A4, D5]], [G2, [G4, B4, D5]],
  [E3, [E4, G4, B4]], [C3, [E4, G4, C5]], [D3, [Fs4, A4, D5]], [G2, [D4, G4, B4]],
];
PROG.forEach(([root, notes], bar) => {
  const s = bar * 16;
  RUN.bass.push([s, root, 3], [s + 4, root + 12, 2], [s + 6, root + 7, 2], [s + 8, root, 3], [s + 12, root + 7, 2], [s + 14, root + 12, 2]);
  RUN.chords.push([s + 2, notes], [s + 6, notes], [s + 10, notes], [s + 14, notes]);
});

/** Relaxed menu tune – slower "harana"-style waltz-ish groove. */
const MENU: Track = {
  bpm: 96,
  steps: 64,
  melody: [
    [0, B4, 4], [4, D5, 2], [6, E5, 2], [8, D5, 6], [14, B4, 2],
    [16, C5, 4], [20, E5, 2], [22, D5, 2], [24, C5, 6], [30, A4, 2],
    [32, B4, 4], [36, G4, 2], [38, A4, 2], [40, B4, 4], [44, D5, 4],
    [48, C5, 3], [51, B4, 1], [52, A4, 4], [56, G4, 8],
  ],
  bass: [],
  chords: [],
  drums: false,
};
[[G2, [G4, B4, D5]], [A2, [E4, A4, C5]], [B2, [D4, G4, B4]], [D3, [Fs4, A4, D5]]].forEach(([root, notes], bar) => {
  const s = bar * 16;
  const r = root as number;
  MENU.bass.push([s, r, 4], [s + 8, r + 7, 4]);
  MENU.chords.push([s + 4, notes as number[]], [s + 12, notes as number[]]);
});

const hz = (m: number) => 440 * Math.pow(2, (m - 69) / 12);

export class Music {
  private track: Track | null = null;
  private step = 0;
  private nextTime = 0;
  private timer: number | null = null;
  private noise: AudioBuffer;
  private intensity = 1;

  constructor(private ctx: AudioContext, private out: AudioNode) {
    const len = ctx.sampleRate * 0.5;
    this.noise = ctx.createBuffer(1, len, ctx.sampleRate);
    const d = this.noise.getChannelData(0);
    for (let i = 0; i < len; i++) d[i] = Math.random() * 2 - 1;
  }

  play(which: 'run' | 'menu'): void {
    const t = which === 'run' ? RUN : MENU;
    if (this.track === t && this.timer !== null) return;
    this.stop();
    this.track = t;
    this.step = 0;
    this.nextTime = this.ctx.currentTime + 0.08;
    this.timer = window.setInterval(() => this.schedule(), 25);
  }

  stop(): void {
    if (this.timer !== null) clearInterval(this.timer);
    this.timer = null;
    this.track = null;
  }

  setIntensity(v: number): void {
    this.intensity = v;
  }

  private schedule(): void {
    const tr = this.track;
    if (!tr) return;
    // if the context was suspended we would schedule a backlog – resync
    if (this.nextTime < this.ctx.currentTime - 0.2) this.nextTime = this.ctx.currentTime + 0.05;
    const stepDur = 60 / tr.bpm / 4;
    while (this.nextTime < this.ctx.currentTime + 0.12) {
      const s = this.step % tr.steps;
      for (const [st, m, len] of tr.melody) if (st === s && m !== null) this.pluck(hz(m), this.nextTime, len * stepDur, 0.16, true);
      for (const [st, m, len] of tr.bass) if (st === s && m !== null) this.bass(hz(m), this.nextTime, len * stepDur);
      for (const [st, notes] of tr.chords) if (st === s) notes.forEach((n) => this.pluck(hz(n), this.nextTime, stepDur * 1.5, 0.045, false));
      if (tr.drums && this.intensity > 0.2) {
        if (s % 8 === 0) this.kick(this.nextTime);
        if (s % 8 === 4) this.snare(this.nextTime);
        if (s % 2 === 0) this.hat(this.nextTime, s % 4 === 2 ? 0.05 : 0.025);
      }
      this.nextTime += stepDur;
      this.step++;
    }
  }

  /** Bandurria-like pluck; long notes use rondalla tremolo (rapid re-plucks). */
  private pluck(f: number, t: number, dur: number, vol: number, tremolo: boolean): void {
    const ctx = this.ctx;
    const repeats = tremolo && dur > 0.2 ? Math.floor(dur / 0.07) : 1;
    for (let r = 0; r < repeats; r++) {
      const st = t + r * 0.07;
      const o = ctx.createOscillator();
      const o2 = ctx.createOscillator();
      const g = ctx.createGain();
      const flt = ctx.createBiquadFilter();
      o.type = 'triangle';
      o2.type = 'square';
      o.frequency.value = f;
      o2.frequency.value = f * 2.002;
      flt.type = 'lowpass';
      flt.frequency.setValueAtTime(f * 6, st);
      flt.frequency.exponentialRampToValueAtTime(f * 1.5, st + 0.15);
      const v = vol * (r === 0 ? 1 : 0.6);
      const d = repeats > 1 ? 0.09 : Math.min(0.5, dur + 0.1);
      g.gain.setValueAtTime(0.0001, st);
      g.gain.exponentialRampToValueAtTime(v, st + 0.004);
      g.gain.exponentialRampToValueAtTime(0.0001, st + d);
      const g2 = ctx.createGain();
      g2.gain.value = 0.25;
      o.connect(g);
      o2.connect(g2).connect(g);
      g.connect(flt).connect(this.out);
      o.start(st);
      o2.start(st);
      o.stop(st + d + 0.02);
      o2.stop(st + d + 0.02);
    }
  }

  private bass(f: number, t: number, dur: number): void {
    const o = this.ctx.createOscillator();
    const g = this.ctx.createGain();
    o.type = 'triangle';
    o.frequency.value = f;
    g.gain.setValueAtTime(0.0001, t);
    g.gain.exponentialRampToValueAtTime(0.22, t + 0.01);
    g.gain.exponentialRampToValueAtTime(0.0001, t + dur * 0.95);
    o.connect(g).connect(this.out);
    o.start(t);
    o.stop(t + dur);
  }

  private kick(t: number): void {
    const o = this.ctx.createOscillator();
    const g = this.ctx.createGain();
    o.frequency.setValueAtTime(140, t);
    o.frequency.exponentialRampToValueAtTime(45, t + 0.12);
    g.gain.setValueAtTime(0.35, t);
    g.gain.exponentialRampToValueAtTime(0.0001, t + 0.18);
    o.connect(g).connect(this.out);
    o.start(t);
    o.stop(t + 0.2);
  }

  private noiseHit(t: number, vol: number, freq: number, dur: number, type: BiquadFilterType): void {
    const src = this.ctx.createBufferSource();
    src.buffer = this.noise;
    const f = this.ctx.createBiquadFilter();
    f.type = type;
    f.frequency.value = freq;
    const g = this.ctx.createGain();
    g.gain.setValueAtTime(vol, t);
    g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    src.connect(f).connect(g).connect(this.out);
    src.start(t, Math.random() * 0.3);
    src.stop(t + dur + 0.01);
  }

  private snare(t: number): void {
    this.noiseHit(t, 0.12, 1800, 0.12, 'bandpass');
  }

  private hat(t: number, vol: number): void {
    this.noiseHit(t, vol, 7000, 0.04, 'highpass');
  }
}
