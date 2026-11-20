import { describe, expect, it } from 'vitest';
import { MemoryStore, SaveManager, checksum, defaultSave, sanitize } from '../../src/save/SaveManager';

describe('SaveManager', () => {
  it('starts with defaults and persists runs', () => {
    const store = new MemoryStore();
    const s = new SaveManager(store);
    expect(s.data.coins).toBe(0);
    const r = s.recordRun({ score: 1234, distance: 800, coins: 40, delivered: 5, pickedUp: 7, crashed: true, seconds: 60 });
    expect(r.newBest).toBe(true);
    const s2 = new SaveManager(store);
    expect(s2.data.bestScore).toBe(1234);
    expect(s2.data.coins).toBe(40);
    expect(s2.data.stats.totalDelivered).toBe(5);
    expect(s2.data.topRuns[0].score).toBe(1234);
  });

  it('does not lower the best score', () => {
    const s = new SaveManager(new MemoryStore());
    s.recordRun({ score: 500, distance: 300, coins: 1, delivered: 0, pickedUp: 0, crashed: true, seconds: 10 });
    const r = s.recordRun({ score: 100, distance: 50, coins: 1, delivered: 0, pickedUp: 0, crashed: true, seconds: 10 });
    expect(r.newBest).toBe(false);
    expect(s.data.bestScore).toBe(500);
    expect(s.data.coins).toBe(2);
  });

  it('recovers from a corrupted main save using the backup', () => {
    const store = new MemoryStore();
    const s = new SaveManager(store);
    s.data.coins = 77;
    s.save();
    s.data.coins = 99;
    s.save(); // backup now holds coins=77
    store.set('jeepneyRush.save', '{"sum":"dead","body":"{\\"coins\\":999999}"}');
    const s2 = new SaveManager(store);
    expect(s2.recovered).toBe('backup');
    expect(s2.data.coins).toBe(77);
  });

  it('resets when everything is corrupted', () => {
    const store = new MemoryStore();
    store.set('jeepneyRush.save', 'not json');
    const s = new SaveManager(store);
    expect(s.recovered).toBe('reset');
    expect(s.data).toMatchObject({ coins: 0, bestScore: 0 });
  });

  it('sanitizes hostile or malformed values', () => {
    const d = sanitize({ coins: -5, bestScore: Infinity, settings: { quality: 'ultra', music: 'yes' }, topRuns: [null, { score: 10 }] });
    expect(d.coins).toBe(0);
    expect(d.bestScore).toBe(0);
    expect(d.settings.quality).toBe('medium');
    expect(d.settings.music).toBe(true);
    expect(d.topRuns).toHaveLength(1);
  });

  it('checksum detects tampering', () => {
    const body = JSON.stringify(defaultSave(0));
    expect(checksum(body)).not.toBe(checksum(body.replace('"coins":0', '"coins":1')));
  });
});
