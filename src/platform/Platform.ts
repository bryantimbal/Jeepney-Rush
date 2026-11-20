import { Capacitor } from '@capacitor/core';
import type { KeyValueStore } from '../save/SaveManager';

/**
 * Thin wrapper over Capacitor plugins so the game runs identically in a
 * desktop browser (development) and inside the Android app.
 */
export const isNative = Capacitor.isNativePlatform();

export interface LifecycleHandlers {
  pause(): void;
  resume(): void;
  back(): void;
}

export async function initPlatform(h: LifecycleHandlers): Promise<void> {
  document.addEventListener('visibilitychange', () => (document.hidden ? h.pause() : h.resume()));
  window.addEventListener('pagehide', () => h.pause());
  if (!isNative) return;
  try {
    const { App } = await import('@capacitor/app');
    App.addListener('pause', () => h.pause());
    App.addListener('resume', () => h.resume());
    App.addListener('backButton', () => h.back());
  } catch (e) {
    console.warn('[platform] app plugin unavailable', e);
  }
  try {
    const { StatusBar } = await import('@capacitor/status-bar');
    await StatusBar.hide();
  } catch {
    /* optional */
  }
  try {
    const { ScreenOrientation } = await import('@capacitor/screen-orientation');
    await ScreenOrientation.lock({ orientation: 'portrait' });
  } catch {
    /* manifest also locks portrait */
  }
}

export async function exitApp(): Promise<void> {
  if (!isNative) return;
  const { App } = await import('@capacitor/app');
  await App.exitApp();
}

let vibrationOn = true;
export function setVibration(on: boolean): void {
  vibrationOn = on;
}

export function haptic(kind: 'light' | 'medium' | 'heavy'): void {
  if (!vibrationOn) return;
  if (isNative) {
    import('@capacitor/haptics')
      .then(({ Haptics, ImpactStyle }) => Haptics.impact({ style: kind === 'light' ? ImpactStyle.Light : kind === 'medium' ? ImpactStyle.Medium : ImpactStyle.Heavy }))
      .catch(() => {});
  } else if (navigator.vibrate) {
    navigator.vibrate(kind === 'light' ? 10 : kind === 'medium' ? 25 : 60);
  }
}

/**
 * localStorage, mirrored into native Preferences on Android (the WebView's
 * localStorage can be cleared by the OS under storage pressure).
 */
export class PlatformStore implements KeyValueStore {
  private mem = new Map<string, string>();
  private hasLocal: boolean;

  constructor() {
    let ok = false;
    try {
      const k = '__jr_probe';
      localStorage.setItem(k, '1');
      localStorage.removeItem(k);
      ok = true;
    } catch {
      ok = false;
    }
    this.hasLocal = ok;
  }

  /** Restore keys from native Preferences when localStorage lost them. */
  async hydrate(keys: string[]): Promise<void> {
    if (!isNative) return;
    try {
      const { Preferences } = await import('@capacitor/preferences');
      for (const k of keys) {
        if (this.get(k)) continue;
        const { value } = await Preferences.get({ key: k });
        if (value) this.setLocal(k, value);
      }
    } catch (e) {
      console.warn('[store] hydrate failed', e);
    }
  }

  get(key: string): string | null {
    if (this.hasLocal) {
      try {
        return localStorage.getItem(key);
      } catch {
        /* fall through */
      }
    }
    return this.mem.get(key) ?? null;
  }

  private setLocal(key: string, value: string): void {
    if (this.hasLocal) {
      try {
        localStorage.setItem(key, value);
        return;
      } catch {
        /* quota – fall back */
      }
    }
    this.mem.set(key, value);
  }

  set(key: string, value: string): void {
    this.setLocal(key, value);
    if (isNative) import('@capacitor/preferences').then(({ Preferences }) => Preferences.set({ key, value })).catch(() => {});
  }
}
