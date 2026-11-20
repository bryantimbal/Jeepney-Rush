/**
 * Central gameplay tuning. Every number a designer may want to tweak lives here
 * so balancing never requires touching system code.
 */
export const LANES = [-2.7, 0, 2.7] as const;
export const LANE_COUNT = LANES.length;
export const ROAD_HALF_WIDTH = 4.4;

export const PLAYER = {
  halfWidth: 0.9,
  halfLength: 2.55,
  laneChangeTime: 0.17, // seconds to slide fully into the next lane
  jumpVelocity: 9.6,
  gravity: 27,
  fastFallGravity: 90,
  maxHealth: 3,
  invulnTime: 1.6,
  capacity: 5,
};

export const SPEED = {
  start: 17, // m/s
  max: 40,
  rampDistance: 2600, // metres until max base speed is reached
  gasMultiplier: 1.22,
  brakeMultiplier: 0.55,
  brakeDuration: 0.9,
  brakeCooldown: 1.4,
  crashSlowdown: 0.45,
};

export const FUEL = {
  max: 100,
  drainPerSecond: 1.15,
  gasDrainMultiplier: 2.0,
  canAmount: 35,
  lowThreshold: 22,
};

export const SPAWN = {
  ahead: 175, // metres ahead of the player where content appears
  despawnBehind: 14,
  minRowGap: 20,
  maxRowGap: 38,
  /** Minimum time (s) between consecutive blocking rows so a human can react. */
  minReactionTime: 0.62,
  stopEvery: [260, 380] as [number, number],
  fuelEvery: [380, 520] as [number, number],
  zoneLength: 16,
};

export const SCORE = {
  perMetre: 1,
  coin: 10,
  pickup: 25,
  delivery: 100,
  gasMultiplier: 1.5,
};

export const REWARD = {
  coinValue: 1,
  pickupCoins: 2,
  fareCoins: 5,
  /** Extra coins per additional passenger dropped at the same stop. */
  groupBonusPerPassenger: 2,
  /** Each consecutive stop served without missing a passenger raises the multiplier. */
  streakStep: 0.25,
  streakMax: 3,
};

/** Difficulty in [0,1] based on distance travelled. */
export function difficultyAt(distance: number): number {
  return Math.min(1, Math.max(0, distance / 3200));
}

export function baseSpeedAt(distance: number): number {
  const t = Math.min(1, distance / SPEED.rampDistance);
  // ease-out so early game ramps quickly then levels off
  const eased = 1 - Math.pow(1 - t, 1.6);
  return SPEED.start + (SPEED.max - SPEED.start) * eased;
}

export const STOP_NAMES = [
  'QUIAPO', 'CUBAO', 'DIVISORIA', 'RECTO', 'LAWTON', 'ESPAÑA', 'TAFT',
  'MONUMENTO', 'BACLARAN', 'BLUMENTRITT', 'KALAW', 'PACO', 'STA. MESA',
  'PEDRO GIL', 'VITO CRUZ', 'LIBERTAD', 'SAMPALOC', 'ERMITA', 'MALATE', 'TONDO',
];
