import type { MeterLocation, TimeBucket } from '../../graphql/generated/enums';
import type { OverviewEnergySparklinesQuery } from '../../graphql/generated/graphql';
import { meterLocations } from '../../domain/meters';
import { bucketGrid, fillBuckets } from '../../domain/timeBuckets';
import type { TimeRange } from '../../domain/timeRanges';

type SparklineRow = OverviewEnergySparklinesQuery['energyStatistics'][number];

export const SPARKLINE_RANGE_MS = 2 * 60 * 60 * 1000;
export const SPARKLINE_BUCKET: TimeBucket = 'FIVE_MINUTES';

export interface EnergySparklines {
  values: ReadonlyMap<MeterLocation, (number | null)[]>;
  /** Shared by all cards, so they can be compared. */
  max: number;
}

export function toEnergySparklines(
  rows: readonly SparklineRow[],
  range: TimeRange,
  bucket: TimeBucket,
): EnergySparklines {
  const grid = bucketGrid(range, bucket);
  let max = 0;

  const values = new Map(
    meterLocations.map((location) => {
      const series = fillBuckets(
        grid,
        rows.filter((row) => row.location === location),
        (row) => row.bucketStart,
      ).map((slot) => slot.value?.energy.average ?? null);

      for (const value of series) {
        max = Math.max(max, value ?? 0);
      }

      return [location, series];
    }),
  );

  return { values, max };
}
