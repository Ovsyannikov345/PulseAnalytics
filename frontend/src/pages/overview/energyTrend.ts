import { gapRuns } from '../../components/charts/gapRuns';
import type { TimeBucket } from '../../graphql/generated/enums';
import type { OverviewEnergyTrendQuery } from '../../graphql/generated/graphql';
import { formatNumber } from '../../domain/formatting';
import { bucketGrid, bucketLengthMs, fillBuckets, timeBucketLabel } from '../../domain/timeBuckets';
import type { TimeRange } from '../../domain/timeRanges';

type TrendRow = OverviewEnergyTrendQuery['energyStatistics'][number];

export const ENERGY_TREND_BUCKET: TimeBucket = 'FIFTEEN_MINUTES';

export interface EnergyBucket {
  average: number;
  minimum: number;
  maximum: number;
  samples: number;
}

export interface EnergyTrendPoint {
  time: number;
  value: number | null;
  bucket: EnergyBucket | null;
}

export function toEnergyTrend(
  rows: readonly TrendRow[],
  range: TimeRange,
  bucket: TimeBucket,
): EnergyTrendPoint[] {
  return fillBuckets(bucketGrid(range, bucket), rows, (row) => row.bucketStart).map(
    ({ start, value: row }) => ({
      time: start.getTime(),
      value: row?.energy.average ?? null,
      bucket: row
        ? {
            average: row.energy.average,
            minimum: row.energy.minimum,
            maximum: row.energy.maximum,
            samples: row.samples,
          }
        : null,
    }),
  );
}

export function describeEnergyTrend(points: readonly EnergyTrendPoint[]): string {
  const averages = points.flatMap((point) => (point.value === null ? [] : [point.value]));
  const gapCount = gapRuns(points, bucketLengthMs(ENERGY_TREND_BUCKET)).length;
  const summary = `Average energy of all locations per ${timeBucketLabel(ENERGY_TREND_BUCKET)} over the last 24 hours, from ${formatNumber(Math.min(...averages), 2)} to ${formatNumber(Math.max(...averages), 2)}.`;

  if (gapCount === 0) {
    return summary;
  }
  return `${summary} No samples in ${String(gapCount)} ${gapCount === 1 ? 'period' : 'periods'}.`;
}
