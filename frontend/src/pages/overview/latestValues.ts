import type { MeterLocation } from '../../graphql/generated/enums';
import type { OverviewLatestReadingsQuery } from '../../graphql/generated/graphql';
import { meterLocations } from '../../domain/meters';

export type LatestReading = OverviewLatestReadingsQuery['latestReadings'][number];

/** One air quality reading's values: CO₂ in ppm, PM2.5 in µg/m³, relative humidity in %. */
export interface AirQualityValues {
  co2: number;
  pm25: number;
  humidity: number;
}

/** One location's newest values; `null` where its meter has no reading yet. */
export interface LocationValues {
  location: MeterLocation;
  energy: number | null;
  airQuality: AirQualityValues | null;
  motionDetected: boolean | null;
}

/** What the Overview shows from the newest reading of every meter. */
export interface LatestValues {
  /** Sum over the locations' energy meters. */
  energyTotal: number | null;
  /** Averages over the locations' air quality meters. */
  averageAirQuality: AirQualityValues | null;
  /** Locations whose motion sensor detected motion, in location order. */
  motionLocations: MeterLocation[];
  /** Locations with a motion sensor reading. */
  motionSensorCount: number;
  /** Every location in the gateway's order. */
  locations: LocationValues[];
  /** When the newest of the readings was received; `null` without readings. */
  latestReceivedAt: Date | null;
}

function sum(values: readonly number[]): number {
  return values.reduce((total, value) => total + value, 0);
}

function averageOf(readings: readonly AirQualityValues[]): AirQualityValues | null {
  if (readings.length === 0) {
    return null;
  }

  const average = (pick: (values: AirQualityValues) => number) =>
    sum(readings.map(pick)) / readings.length;
  return {
    co2: average((values) => values.co2),
    pm25: average((values) => values.pm25),
    humidity: average((values) => values.humidity),
  };
}

export function summarizeLatest(readings: readonly LatestReading[]): LatestValues {
  const locations = new Map<MeterLocation, LocationValues>(
    meterLocations.map((location) => [
      location,
      { location, energy: null, airQuality: null, motionDetected: null },
    ]),
  );
  let latestReceivedAtMs: number | null = null;

  for (const reading of readings) {
    // A location this app doesn't know yet (the schema grew): skipped rather than shown wrong.
    const values = locations.get(reading.location);
    if (!values) {
      continue;
    }

    switch (reading.__typename) {
      case 'EnergyReading':
        values.energy = reading.energy;
        break;
      case 'AirQualityReading':
        values.airQuality = { co2: reading.co2, pm25: reading.pm25, humidity: reading.humidity };
        break;
      case 'MotionReading':
        values.motionDetected = reading.motionDetected;
        break;
    }

    const receivedAtMs = new Date(reading.receivedAt).getTime();
    latestReceivedAtMs = Math.max(latestReceivedAtMs ?? receivedAtMs, receivedAtMs);
  }

  const all = [...locations.values()];
  const energies = all.flatMap((values) => (values.energy === null ? [] : [values.energy]));

  return {
    energyTotal: energies.length > 0 ? sum(energies) : null,
    averageAirQuality: averageOf(
      all.flatMap((values) => (values.airQuality ? [values.airQuality] : [])),
    ),
    motionLocations: all
      .filter((values) => values.motionDetected === true)
      .map((values) => values.location),
    motionSensorCount: all.filter((values) => values.motionDetected !== null).length,
    locations: all,
    latestReceivedAt: latestReceivedAtMs === null ? null : new Date(latestReceivedAtMs),
  };
}
