/** Small, fast seeded PRNG (mulberry32) so runs and tests are reproducible. */
export class Random {
  private s: number;
  constructor(seed = Date.now() >>> 0) {
    this.s = seed >>> 0;
  }
  next(): number {
    let t = (this.s += 0x6d2b79f5);
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  }
  range(min: number, max: number): number {
    return min + (max - min) * this.next();
  }
  int(min: number, maxInclusive: number): number {
    return Math.floor(this.range(min, maxInclusive + 1));
  }
  chance(p: number): boolean {
    return this.next() < p;
  }
  pick<T>(arr: readonly T[]): T {
    return arr[Math.floor(this.next() * arr.length)];
  }
  weighted<T>(items: readonly { item: T; w: number }[]): T {
    let total = 0;
    for (const i of items) total += Math.max(0, i.w);
    let r = this.next() * total;
    for (const i of items) {
      r -= Math.max(0, i.w);
      if (r <= 0) return i.item;
    }
    return items[items.length - 1].item;
  }
}
