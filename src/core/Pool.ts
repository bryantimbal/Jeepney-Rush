/** Generic object pool – avoids GC spikes from allocating meshes during a run. */
export class Pool<T> {
  private free: T[] = [];
  readonly active: T[] = [];
  constructor(private factory: () => T, private onAcquire?: (o: T) => void, private onRelease?: (o: T) => void) {}

  prewarm(n: number): void {
    for (let i = 0; i < n; i++) this.free.push(this.factory());
  }
  acquire(): T {
    const o = this.free.pop() ?? this.factory();
    this.active.push(o);
    this.onAcquire?.(o);
    return o;
  }
  release(o: T): void {
    const i = this.active.indexOf(o);
    if (i < 0) return;
    // swap-remove, order of active objects is irrelevant
    const last = this.active.pop()!;
    if (i < this.active.length) this.active[i] = last;
    this.onRelease?.(o);
    this.free.push(o);
  }
  releaseAll(): void {
    while (this.active.length) this.release(this.active[this.active.length - 1]);
  }
  get size(): number {
    return this.active.length + this.free.length;
  }
}
