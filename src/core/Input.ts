export type Action = 'left' | 'right' | 'up' | 'down' | 'horn' | 'pause';

/**
 * Unified input: swipes + touch-and-hold on mobile, mirrored keyboard controls
 * on desktop. Swipes fire as soon as the finger travels far enough (no waiting
 * for touchend), which is what makes runners feel responsive.
 */
export class Input {
  enabled = false;
  holding = false;
  private keyHold = false;
  private touchHold = false;
  private listeners: ((a: Action) => void)[] = [];
  private startX = 0;
  private startY = 0;
  private pointerId: number | null = null;
  private swiped = false;
  private holdTimer: number | null = null;
  private readonly SWIPE_PX: number;

  constructor(surface: HTMLElement) {
    this.SWIPE_PX = Math.max(24, Math.min(window.innerWidth, window.innerHeight) * 0.06);
    surface.addEventListener('pointerdown', this.onDown, { passive: false });
    window.addEventListener('pointermove', this.onMove, { passive: false });
    window.addEventListener('pointerup', this.onUp);
    window.addEventListener('pointercancel', this.onUp);
    window.addEventListener('keydown', this.onKey);
    window.addEventListener('keyup', this.onKeyUp);
    window.addEventListener('blur', () => this.reset());
  }

  onAction(fn: (a: Action) => void): void {
    this.listeners.push(fn);
  }

  private fire(a: Action): void {
    if (!this.enabled && a !== 'pause') return;
    for (const l of this.listeners) l(a);
  }

  reset(): void {
    this.keyHold = this.touchHold = this.holding = false;
    this.pointerId = null;
    if (this.holdTimer) clearTimeout(this.holdTimer);
  }

  private onDown = (e: PointerEvent): void => {
    if ((e.target as HTMLElement).closest('button, [data-ui]')) return;
    if (this.pointerId !== null) return;
    e.preventDefault();
    this.pointerId = e.pointerId;
    this.startX = e.clientX;
    this.startY = e.clientY;
    this.swiped = false;
    if (this.holdTimer) clearTimeout(this.holdTimer);
    this.holdTimer = window.setTimeout(() => {
      if (this.pointerId !== null && !this.swiped) {
        this.touchHold = true;
        this.updateHold();
      }
    }, 200);
  };

  private onMove = (e: PointerEvent): void => {
    if (e.pointerId !== this.pointerId) return;
    const dx = e.clientX - this.startX;
    const dy = e.clientY - this.startY;
    const ax = Math.abs(dx), ay = Math.abs(dy);
    if (Math.max(ax, ay) < this.SWIPE_PX) return;
    this.swiped = true;
    this.touchHold = false;
    this.updateHold();
    if (ax > ay) this.fire(dx < 0 ? 'left' : 'right');
    else this.fire(dy < 0 ? 'up' : 'down');
    // re-arm so a finger can chain swipes (e.g. left-left) without lifting
    this.startX = e.clientX;
    this.startY = e.clientY;
  };

  private onUp = (e: PointerEvent): void => {
    if (e.pointerId !== this.pointerId) return;
    this.pointerId = null;
    if (this.holdTimer) clearTimeout(this.holdTimer);
    this.touchHold = false;
    this.updateHold();
  };

  private onKey = (e: KeyboardEvent): void => {
    if (e.repeat && !['ArrowLeft', 'ArrowRight'].includes(e.code)) return;
    const map: Record<string, Action> = {
      ArrowLeft: 'left', KeyA: 'left',
      ArrowRight: 'right', KeyD: 'right',
      ArrowUp: 'up', KeyW: 'up', Space: 'up',
      ArrowDown: 'down', KeyS: 'down',
      KeyH: 'horn',
      Escape: 'pause', KeyP: 'pause',
    };
    if (e.code === 'ShiftLeft' || e.code === 'ShiftRight') {
      this.keyHold = true;
      this.updateHold();
      return;
    }
    const a = map[e.code];
    if (!a) return;
    if (e.repeat) return;
    if ((e.target as HTMLElement)?.tagName === 'INPUT') return;
    e.preventDefault();
    this.fire(a);
  };

  private onKeyUp = (e: KeyboardEvent): void => {
    if (e.code === 'ShiftLeft' || e.code === 'ShiftRight') {
      this.keyHold = false;
      this.updateHold();
    }
  };

  private updateHold(): void {
    this.holding = this.enabled && (this.keyHold || this.touchHold);
  }
}
