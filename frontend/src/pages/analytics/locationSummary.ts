import type { MeterLocation } from '../../graphql/generated/enums';
import type {
  AnalyticsAirQualitySummaryQuery,
  AnalyticsEnergySummaryQuery,
  AnalyticsMotionSummaryQuery,
} from '../../graphql/generated/graphql';

/** One operation's values per location; a location without readings has no entry. */
export type ByLocation<T> = Partial<Record<MeterLocation, T>>;

export interface EnergySummary {
  average: number;
  minimum: number;
  maximum: number;
}

export interface AirQualitySummary {
  co2: number;
  pm25: number;
  humidity: number;
}

export interface MotionSummary {
  activityRatio: number;
  samples: number;
}

function byLocation<TRow extends { location: MeterLocation | null }, T>(
  rows: readonly TRow[],
  pick: (row: TRow) => T,
): ByLocation<T> {
  const values: ByLocation<T> = {};
  for (const row of rows) {
    if (row.location) {
      values[row.location] = pick(row);
    }
  }
  return values;
}

export function toEnergySummary(
  rows: AnalyticsEnergySummaryQuery['energyStatistics'],
): ByLocation<EnergySummary> {
  return byLocation(rows, ({ energy }) => ({
    average: energy.average,
    minimum: energy.minimum,
    maximum: energy.maximum,
  }));
}

export function toAirQualitySummary(
  rows: AnalyticsAirQualitySummaryQuery['airQualityStatistics'],
): ByLocation<AirQualitySummary> {
  return byLocation(rows, (row) => ({
    co2: row.co2.average,
    pm25: row.pm25.average,
    humidity: row.humidity.average,
  }));
}

export function toMotionSummary(
  rows: AnalyticsMotionSummaryQuery['motionStatistics'],
): ByLocation<MotionSummary> {
  return byLocation(rows, (row) => ({ activityRatio: row.activityRatio, samples: row.samples }));
}

export interface LocationSummaryRow {
  location: MeterLocation;
  /** `null` where the location has no readings or the operation failed. */
  energy: EnergySummary | null;
  airQuality: AirQualitySummary | null;
  motion: MotionSummary | null;
}

export interface SummaryParts {
  /** `null` for an operation that failed. */
  energy: ByLocation<EnergySummary> | null;
  airQuality: ByLocation<AirQualitySummary> | null;
  motion: ByLocation<MotionSummary> | null;
}

/** One row per location, joining the three operations' values. */
export function toLocationSummary(
  locations: readonly MeterLocation[],
  { energy, airQuality, motion }: SummaryParts,
): LocationSummaryRow[] {
  return locations.map((location) => ({
    location,
    energy: energy?.[location] ?? null,
    airQuality: airQuality?.[location] ?? null,
    motion: motion?.[location] ?? null,
  }));
}
