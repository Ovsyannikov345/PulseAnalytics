import type { MeterLocation, TimeBucket } from '../../graphql/generated/enums';
import type { OverviewEnergySparklinesQuery } from '../../graphql/generated/graphql';
import { meterLocations } from '../../domain/meters';
import { bucketGrid, fillBuckets } from '../../domain/timeBuckets';
import type { TimeRange } from '../../domain/timeRanges';

type SparklineRow = OverviewEnergySparklinesQuery['energyStatistics'][number];

/** The location cards' trend: the last 2 hours in 5-minute averages. */
export const SPARKLINE_RANGE_MS = 2 * 60 * 60 * 1000;
export const SPARKLINE_BUCKET: TimeBucket = 'FIVE_MINUTES';

export interface EnergySparklines {
  /** Per location, the average energy of every bucket in the range; `null` where it had no samples. */
  values: ReadonlyMap<MeterLocation, (number | null)[]>;
  /** The highest value of all locations, so the cards share one scale and can be compared. */
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
