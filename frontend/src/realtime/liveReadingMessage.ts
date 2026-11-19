import type { LiveReading } from '../domain/liveReadings';
import { meterLocationFromRestName, meterTypeFromRestName } from '../domain/meters';

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function isFiniteNumber(value: unknown): value is number {
  return typeof value === 'number' && Number.isFinite(value);
}

/**
 * Reads one `ReadingStored` message (`{"type":"AirQuality","location":"LivingRoom",…}`, C# enum
 * names) into a reading. Anything that doesn't have the hub's shape is dropped (`null`): an
 * unknown type or location, a missing or mistyped field, a time that doesn't parse.
 */
export function parseLiveReading(message: unknown): LiveReading | null {
  if (!isRecord(message)) {
    return null;
  }

  const { type, id, location, receivedAt } = message;
  if (
    typeof type !== 'string' ||
    typeof id !== 'string' ||
    typeof location !== 'string' ||
    typeof receivedAt !== 'string'
  ) {
    return null;
  }

  const meterType = meterTypeFromRestName(type);
  const meterLocation = meterLocationFromRestName(location);
  if (!meterType || !meterLocation || id === '' || Number.isNaN(Date.parse(receivedAt))) {
    return null;
  }

  const base = { id, location: meterLocation, receivedAt };
  switch (meterType) {
    case 'MOTION': {
      const { motionDetected } = message;
      return typeof motionDetected === 'boolean'
        ? { ...base, type: 'MOTION', motionDetected }
        : null;
    }
    case 'ENERGY': {
      const { energy } = message;
      return isFiniteNumber(energy) ? { ...base, type: 'ENERGY', energy } : null;
    }
    case 'AIR_QUALITY': {
      const { co2, pm25, humidity } = message;
      return isFiniteNumber(co2) && isFiniteNumber(pm25) && isFiniteNumber(humidity)
        ? { ...base, type: 'AIR_QUALITY', co2, pm25, humidity }
        : null;
    }
  }
}
