type Handler<T> = (payload: T) => void;

/** Minimal typed event bus used to decouple gameplay systems from UI/audio. */
export class Emitter<M extends Record<string, unknown>> {
  private handlers: { [K in keyof M]?: Handler<M[K]>[] } = {};
  on<K extends keyof M>(type: K, fn: Handler<M[K]>): () => void {
    (this.handlers[type] ??= []).push(fn);
    return () => this.off(type, fn);
  }
  off<K extends keyof M>(type: K, fn: Handler<M[K]>): void {
    const list = this.handlers[type];
    if (!list) return;
    const i = list.indexOf(fn);
    if (i >= 0) list.splice(i, 1);
  }
  emit<K extends keyof M>(type: K, payload: M[K]): void {
    const list = this.handlers[type];
    if (!list) return;
    for (const fn of list.slice()) {
      try {
        fn(payload);
      } catch (err) {
        console.error(`[events] handler for "${String(type)}" failed`, err);
      }
    }
  }
}

export type GameEvents = {
  coin: { value: number; x: number; y: number; z: number };
  fuel: { amount: number };
  pickup: { count: number; stopName: string };
  deliver: { count: number; coins: number; stopName: string; streak: number };
  missed: { count: number; stopName: string };
  full: { stopName: string };
  hit: { health: number; kind: string };
  shieldBlock: Record<string, never>;
  jump: Record<string, never>;
  land: Record<string, never>;
  lane: { dir: number };
  brake: Record<string, never>;
  honk: Record<string, never>;
  lowFuel: Record<string, never>;
  gameOver: { reason: 'crash' | 'fuel' };
};
