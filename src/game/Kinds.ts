/** Physical definitions for every hazard the player can meet on the road. */
export type VehicleKind = 'car' | 'taxi' | 'jeepney' | 'bus' | 'truck' | 'motorcycle' | 'tricycle';
export type StaticKind = 'cone' | 'barrier' | 'pothole' | 'roadblock' | 'parkedCar' | 'parkedJeepney';
export type HazardKind = VehicleKind | StaticKind;

export interface HazardDef {
  halfW: number;
  halfL: number;
  height: number;
  jumpable: boolean;
  /** Own forward speed range in m/s (0 for static). */
  speed: [number, number];
  label: string;
}

export const HAZARDS: Record<HazardKind, HazardDef> = {
  car: { halfW: 0.92, halfL: 2.2, height: 1.5, jumpable: false, speed: [7, 12], label: 'Car' },
  taxi: { halfW: 0.92, halfL: 2.2, height: 1.6, jumpable: false, speed: [7, 11], label: 'Taxi' },
  jeepney: { halfW: 1.0, halfL: 2.8, height: 2.4, jumpable: false, speed: [6, 10], label: 'Jeepney' },
  bus: { halfW: 1.18, halfL: 5.4, height: 3.3, jumpable: false, speed: [5, 9], label: 'Bus' },
  truck: { halfW: 1.12, halfL: 3.6, height: 3.1, jumpable: false, speed: [5, 8], label: 'Truck' },
  motorcycle: { halfW: 0.42, halfL: 1.0, height: 1.5, jumpable: false, speed: [9, 13], label: 'Motorcycle' },
  tricycle: { halfW: 0.85, halfL: 1.25, height: 1.7, jumpable: false, speed: [4, 7], label: 'Tricycle' },
  cone: { halfW: 0.32, halfL: 0.32, height: 0.78, jumpable: true, speed: [0, 0], label: 'Cone' },
  barrier: { halfW: 1.15, halfL: 0.3, height: 1.0, jumpable: true, speed: [0, 0], label: 'Barrier' },
  pothole: { halfW: 0.95, halfL: 0.85, height: 0.3, jumpable: true, speed: [0, 0], label: 'Pothole' },
  roadblock: { halfW: 1.25, halfL: 0.45, height: 2.4, jumpable: false, speed: [0, 0], label: 'Road block' },
  parkedCar: { halfW: 0.92, halfL: 2.2, height: 1.5, jumpable: false, speed: [0, 0], label: 'Parked car' },
  parkedJeepney: { halfW: 1.0, halfL: 2.8, height: 2.4, jumpable: false, speed: [0, 0], label: 'Parked jeepney' },
};

export function isVehicle(k: HazardKind): k is VehicleKind {
  return HAZARDS[k].speed[1] > 0;
}
