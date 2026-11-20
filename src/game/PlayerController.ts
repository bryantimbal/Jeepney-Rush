import * as THREE from 'three';
import { LANES, PLAYER, SPEED } from '../config';
import type { JeepneyModel } from '../art/JeepneyFactory';

/**
 * Jeepney movement: discrete lanes with eased transitions, jump with
 * fast-fall, brake maneuver, health and invulnerability frames.
 */
export class PlayerController {
  lane = 1;
  x: number = LANES[1];
  y = 0;
  vy = 0;
  grounded = true;
  health: number = PLAYER.maxHealth;
  maxHealth: number = PLAYER.maxHealth;
  invuln = 0;
  brakeT = 0;
  brakeCooldown = 0;
  private laneFrom = 1;
  private laneT = 1;
  private queuedLane: number | null = null;
  private tilt = 0;
  private pitch = 0;
  private wheelSpin = 0;
  private bounceT = 0;
  private time = 0;
  private fastFall = false;

  constructor(public model: JeepneyModel) {}

  reset(): void {
    this.lane = this.laneFrom = 1;
    this.laneT = 1;
    this.x = LANES[1];
    this.y = this.vy = 0;
    this.grounded = true;
    this.health = this.maxHealth;
    this.invuln = 0;
    this.brakeT = this.brakeCooldown = 0;
    this.queuedLane = null;
    this.fastFall = false;
    this.model.root.position.set(this.x, 0, 0);
    this.model.root.visible = true;
  }

  /** Lanes the jeepney body currently overlaps. */
  occupiedLanes(): number[] {
    return this.changingLane ? [this.laneFrom, this.lane] : [this.lane];
  }

  get changingLane(): boolean {
    return this.laneT < 1;
  }

  /** Returns false when the move is impossible (already at the edge). */
  move(dir: -1 | 1): boolean {
    const base = this.changingLane && this.queuedLane === null ? this.lane : this.queuedLane ?? this.lane;
    const target = base + dir;
    if (target < 0 || target >= LANES.length) return false;
    if (this.changingLane) {
      // buffer one extra input so quick double-swipes feel right
      this.queuedLane = target;
      return true;
    }
    this.startLaneChange(target);
    return true;
  }

  private startLaneChange(target: number): void {
    this.laneFrom = this.lane;
    this.lane = target;
    this.laneT = 0;
  }

  jump(): boolean {
    if (!this.grounded) return false;
    this.vy = PLAYER.jumpVelocity;
    this.grounded = false;
    this.fastFall = false;
    return true;
  }

  /** Swipe down: emergency brake ("Para!") or slam back to the road mid-jump. */
  brake(): boolean {
    if (!this.grounded) {
      this.fastFall = true;
      return true;
    }
    if (this.brakeCooldown > 0) return false;
    this.brakeT = SPEED.brakeDuration;
    this.brakeCooldown = SPEED.brakeCooldown;
    return true;
  }

  /** Knocked back toward the lane we came from after a side-swipe. */
  bounceBack(): void {
    if (!this.changingLane) return;
    const from = this.laneFrom;
    this.laneFrom = this.lane;
    this.lane = from;
    this.laneT = 1 - this.laneT;
    this.queuedLane = null;
    this.bounceT = 0.3;
  }

  damage(): void {
    this.health = Math.max(0, this.health - 1);
    this.invuln = PLAYER.invulnTime;
  }

  /** Returns 'land' on the frame the jeepney touches down. */
  update(dt: number, speed: number, gas: boolean): 'land' | null {
    let event: 'land' | null = null;
    this.time += dt;
    if (this.invuln > 0) this.invuln -= dt;
    if (this.brakeT > 0) this.brakeT -= dt;
    if (this.brakeCooldown > 0) this.brakeCooldown -= dt;
    if (this.bounceT > 0) this.bounceT -= dt;

    // lateral
    if (this.laneT < 1) {
      this.laneT = Math.min(1, this.laneT + dt / PLAYER.laneChangeTime);
      if (this.laneT >= 1 && this.queuedLane !== null) {
        const q = this.queuedLane;
        this.queuedLane = null;
        if (q >= 0 && q < LANES.length && q !== this.lane) this.startLaneChange(q);
      }
    }
    const e = 1 - Math.pow(1 - this.laneT, 3);
    const prevX = this.x;
    this.x = LANES[this.laneFrom] + (LANES[this.lane] - LANES[this.laneFrom]) * e;
    const vx = (this.x - prevX) / Math.max(dt, 1e-4);

    // vertical
    if (!this.grounded) {
      this.vy -= (this.fastFall ? PLAYER.fastFallGravity : PLAYER.gravity) * dt;
      this.y += this.vy * dt;
      if (this.y <= 0) {
        this.y = 0;
        this.vy = 0;
        this.grounded = true;
        this.fastFall = false;
        event = 'land';
        this.pitch = 0.06;
      }
    }

    // ---- visuals
    const m = this.model;
    m.root.position.set(this.x, this.y, 0);
    const targetTilt = THREE.MathUtils.clamp(-vx * 0.012, -0.22, 0.22);
    this.tilt += (targetTilt - this.tilt) * Math.min(1, dt * 14);
    const targetPitch = this.grounded ? (this.brakeT > 0 ? 0.045 : gas ? -0.03 : 0) : THREE.MathUtils.clamp(-this.vy * 0.012, -0.12, 0.12);
    this.pitch += (targetPitch - this.pitch) * Math.min(1, dt * 8);
    m.root.rotation.y = -vx * 0.004;
    m.body.rotation.z = this.tilt;
    m.body.rotation.x = this.pitch;
    // diesel idle shake & road bumps
    m.body.position.y = this.grounded ? Math.sin(this.time * 38) * 0.012 + Math.max(0, Math.sin(this.time * 7.3)) * 0.02 : 0;
    this.wheelSpin -= (speed * dt) / 0.43;
    for (const w of m.wheels) w.rotation.x = this.wheelSpin;
    const blink = this.invuln > 0 && Math.floor(this.invuln * 12) % 2 === 0;
    m.body.visible = !blink;
    const blob = m.root.getObjectByName('blob');
    if (blob) {
      blob.position.y = -this.y + 0.02;
      blob.scale.setScalar(1 / (1 + this.y * 0.25));
    }
    (m.brakeLights.material as THREE.MeshBasicMaterial).opacity = this.brakeT > 0 ? 0.9 : 0;
    return event;
  }
}
