import {
  type LiveReading,
  type LiveReadings,
  mergeReadings,
  noLiveReadings,
} from '../../domain/liveReadings';
import type { LiveLatestReadingsQuery } from '../../graphql/generated/graphql';

export type LatestReadingRow = LiveLatestReadingsQuery['latestReadings'][number];

export function toLiveReading(row: LatestReadingRow): LiveReading {
  const base = { id: row.id, location: row.location, receivedAt: row.receivedAt };
  switch (row.__typename) {
    case 'MotionReading':
      return { ...base, type: 'MOTION', motionDetected: row.motionDetected };
    case 'EnergyReading':
      return { ...base, type: 'ENERGY', energy: row.energy };
    case 'AirQualityReading':
      return { ...base, type: 'AIR_QUALITY', co2: row.co2, pm25: row.pm25, humidity: row.humidity };
  }
}

export function toLiveReadings(rows: readonly LatestReadingRow[]): LiveReadings {
  return mergeReadings(noLiveReadings, rows.map(toLiveReading));
}
