import type { TimePoint } from '../../components/charts/TimeSeriesChart';
import { bucketGrid, groupBuckets } from '../../domain/timeBuckets';
import type { TimeRange } from '../../domain/timeRanges';
import type { MeterLocation, TimeBucket } from '../../graphql/generated/enums';
import type { AnalyticsEnergyQuery } from '../../graphql/generated/graphql';

type EnergyRow = AnalyticsEnergyQuery['energyStatistics'][number];

export interface EnergyStats {
  average: number;
  minimum: number;
  maximum: number;
  samples: number;
}

export interface EnergyPoint extends TimePoint {
  byLocation: Partial<Record<MeterLocation, EnergyStats>>;
  /** All locations of the bucket together; `null` when none has samples (a gap). */
  combined: EnergyStats | null;
}

/** The points with the filters they were loaded for, which also give the chart its axis. */
export interface EnergySeries {
  range: TimeRange;
  bucket: TimeBucket;
  location: MeterLocation | null;
  points: EnergyPoint[];
}

/**
 * The statistics of several groups as one. The gateway's own average over all readings is the
 * sample-weighted mean of the groups' averages, so this matches `groupBy: [TIME]` exactly.
 */
export function combineEnergy(groups: readonly EnergyStats[]): EnergyStats | null {
  if (groups.length === 0) {
    return null;
  }

  const samples = groups.reduce((sum, group) => sum + group.samples, 0);
  return {
    average: groups.reduce((sum, group) => sum + group.average * group.samples, 0) / samples,
    minimum: Math.min(...groups.map((group) => group.minimum)),
    maximum: Math.max(...groups.map((group) => group.maximum)),
    samples,
  };
}

export function toEnergySeries(
  rows: readonly EnergyRow[],
  range: TimeRange,
  bucket: TimeBucket,
  location: MeterLocation | null,
): EnergySeries {
  const points = groupBuckets(bucketGrid(range, bucket), rows, (row) => row.bucketStart).map(
    ({ start, rows: bucketRows }) => {
      const byLocation: Partial<Record<MeterLocation, EnergyStats>> = {};
      for (const row of bucketRows) {
        if (row.location) {
          const { average, minimum, maximum } = row.energy;
          byLocation[row.location] = { average, minimum, maximum, samples: row.samples };
        }
      }
      return {
        time: start.getTime(),
        byLocation,
        combined: combineEnergy(Object.values(byLocation)),
      };
    },
  );

  return { range, bucket, location, points };
}
