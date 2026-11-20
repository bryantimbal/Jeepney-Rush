import { REWARD, SCORE } from '../../config';
import type { Random } from '../../core/Random';

export interface Stop {
  id: number;
  name: string;
  side: -1 | 1; // -1 = left curb, +1 = right curb
  lane: number;
  /** World distance (metres from run start) of the zone centre. */
  at: number;
  waiting: number;
  resolved: boolean;
}

export interface ServeResult {
  alighted: number;
  boarded: number;
  leftBehind: number;
  coins: number;
  score: number;
  streak: number;
}

/**
 * Pure passenger bookkeeping: which stops exist, who is aboard and where they
 * are going. Rendering lives in PassengerManager; this class is unit tested.
 */
export class PassengerRoute {
  readonly stops: Stop[] = [];
  /** Destination stop id for each passenger aboard. */
  readonly aboard: number[] = [];
  streak = 0;
  delivered = 0;
  pickedUp = 0;
  missed = 0;
  private nextId = 1;
  private lastName = '';

  constructor(public capacity: number, private rng: Random, private names: readonly string[], public fareMultiplier = 1) {}

  /** Plan a new stop at world distance `at`. Returns it. */
  plan(at: number, side: -1 | 1, lane: number, waiting: number): Stop {
    let name = this.rng.pick(this.names);
    let guard = 0;
    while ((name === this.lastName || this.stops.some((s) => !s.resolved && s.name === name)) && guard++ < 20) name = this.rng.pick(this.names);
    this.lastName = name;
    const stop: Stop = { id: this.nextId++, name, side, lane, at, waiting, resolved: false };
    this.stops.push(stop);
    return stop;
  }

  get count(): number {
    return this.aboard.length;
  }

  alightingAt(stopId: number): number {
    let n = 0;
    for (const d of this.aboard) if (d === stopId) n++;
    return n;
  }

  upcoming(): Stop[] {
    return this.stops.filter((s) => !s.resolved);
  }

  /** Player drove through the stop zone. */
  serve(stop: Stop): ServeResult {
    stop.resolved = true;
    const alighted = this.removeDest(stop.id);
    let coins = 0;
    let score = 0;
    if (alighted > 0) {
      this.streak = Math.min(this.streak + 1, Math.round(REWARD.streakMax / REWARD.streakStep));
      const mult = Math.min(REWARD.streakMax, 1 + (this.streak - 1) * REWARD.streakStep) * this.fareMultiplier;
      coins += Math.round((alighted * REWARD.fareCoins + (alighted - 1) * REWARD.groupBonusPerPassenger) * mult);
      score += Math.round(alighted * SCORE.delivery * mult);
      this.delivered += alighted;
    }
    const space = this.capacity - this.aboard.length;
    const boarded = Math.max(0, Math.min(space, stop.waiting));
    const futures = this.stops.filter((s) => !s.resolved && s.at > stop.at).sort((a, b) => a.at - b.at).slice(0, 3);
    for (let i = 0; i < boarded; i++) {
      // Most passengers ride to one of the next two stops, some go further.
      const dest = futures.length ? futures[Math.min(futures.length - 1, Math.floor(this.rng.next() * this.rng.next() * futures.length * 1.6))] : undefined;
      if (dest) this.aboard.push(dest.id);
    }
    const actuallyBoarded = futures.length ? boarded : 0;
    coins += actuallyBoarded * REWARD.pickupCoins;
    score += actuallyBoarded * SCORE.pickup;
    this.pickedUp += actuallyBoarded;
    const leftBehind = stop.waiting - actuallyBoarded;
    stop.waiting = leftBehind;
    this.prune();
    return { alighted, boarded: actuallyBoarded, leftBehind, coins, score, streak: this.streak };
  }

  /** The stop zone passed without the jeepney in it. Returns passengers lost. */
  miss(stop: Stop): number {
    stop.resolved = true;
    const lost = this.removeDest(stop.id);
    if (lost > 0) {
      this.streak = 0;
      this.missed += lost;
    }
    this.prune();
    return lost;
  }

  private removeDest(id: number): number {
    let n = 0;
    for (let i = this.aboard.length - 1; i >= 0; i--) {
      if (this.aboard[i] === id) {
        this.aboard.splice(i, 1);
        n++;
      }
    }
    return n;
  }

  private prune(): void {
    // keep resolved stops only while someone could still reference them (never) – drop old ones
    for (let i = this.stops.length - 1; i >= 0; i--) if (this.stops[i].resolved) this.stops.splice(i, 1);
  }
}
