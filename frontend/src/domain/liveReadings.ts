import type { MeterLocation, MeterType } from '../graphql/generated/enums';
import { meterLocations } from './meters';

interface LiveReadingBase {
  /** The stored reading's id: GraphQL's `MeterReading.id` and the hub message's `id`. */
  id: string;
  location: MeterLocation;
  /** ISO 8601 with an offset; GraphQL and the hub may use different offsets for one instant. */
  receivedAt: string;
}

export type MotionLiveReading = LiveReadingBase & { type: 'MOTION'; motionDetected: boolean };

export type EnergyLiveReading = LiveReadingBase & { type: 'ENERGY'; energy: number };

export type AirQualityLiveReading = LiveReadingBase & {
  type: 'AIR_QUALITY';
  co2: number;
  pm25: number;
  humidity: number;
};

/** The newest sample of one meter, from the `latestReadings` query or a hub push. */
export type LiveReading = MotionLiveReading | EnergyLiveReading | AirQualityLiveReading;

/** At most one reading per meter, keyed by `meterKey`. */
export type LiveReadings = ReadonlyMap<string, LiveReading>;

export const noLiveReadings: LiveReadings = new Map<string, LiveReading>();

export function meterKey(type: MeterType, location: MeterLocation): string {
  return `${type}:${location}`;
}

/**
 * Keeps, per meter, the reading received last. A reading that isn't strictly newer than the one
 * kept (the same reading again, a redelivered push, an older query row) changes nothing, so the
 * order in which the query and the pushes arrive doesn't matter. Returns `current` itself when
 * nothing changed.
 */
export function mergeReadings(
  current: LiveReadings,
  incoming: Iterable<LiveReading>,
): LiveReadings {
  let merged: Map<string, LiveReading> | null = null;

  for (const reading of incoming) {
    const key = meterKey(reading.type, reading.location);
    const kept = (merged ?? current).get(key);
    if (kept && Date.parse(reading.receivedAt) <= Date.parse(kept.receivedAt)) {
      continue;
    }

    merged ??= new Map(current);
    merged.set(key, reading);
  }

  return merged ?? current;
}

export interface RoomView {
  location: MeterLocation;
  /** `null` until the room's meter has a sample. */
  energy: number | null;
  airQuality: { co2: number; pm25: number; humidity: number } | null;
  motionDetected: boolean | null;
}

/** One view per room, in the gateway's location order. */
export function toRoomViews(readings: LiveReadings): RoomView[] {
  return meterLocations.map((location) => {
    const energy = readings.get(meterKey('ENERGY', location));
    const airQuality = readings.get(meterKey('AIR_QUALITY', location));
    const motion = readings.get(meterKey('MOTION', location));

    return {
      location,
      energy: energy?.type === 'ENERGY' ? energy.energy : null,
      airQuality:
        airQuality?.type === 'AIR_QUALITY'
          ? { co2: airQuality.co2, pm25: airQuality.pm25, humidity: airQuality.humidity }
          : null,
      motionDetected: motion?.type === 'MOTION' ? motion.motionDetected : null,
    };
  });
}

export function newestReceivedAt(readings: LiveReadings): Date | null {
  let newest: number | null = null;
  for (const reading of readings.values()) {
    const receivedAt = Date.parse(reading.receivedAt);
    if (newest === null || receivedAt > newest) {
      newest = receivedAt;
    }
  }

  return newest === null ? null : new Date(newest);
}
