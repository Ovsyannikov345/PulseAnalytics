import type { MeterLocation } from '../../graphql/generated/enums';
import type { OverviewLatestReadingsQuery } from '../../graphql/generated/graphql';
import { meterLocations } from '../../domain/meters';

export type LatestReading = OverviewLatestReadingsQuery['latestReadings'][number];

export interface AirQualityValues {
  co2: number;
  pm25: number;
  humidity: number;
}

export interface LocationValues {
  location: MeterLocation;
  energy: number | null;
  airQuality: AirQualityValues | null;
  motionDetected: boolean | null;
}

export interface LatestValues {
  energyTotal: number | null;
  averageAirQuality: AirQualityValues | null;
  motionLocations: MeterLocation[];
  motionSensorCount: number;
  locations: LocationValues[];
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
    // A location added to the schema after this build.
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
