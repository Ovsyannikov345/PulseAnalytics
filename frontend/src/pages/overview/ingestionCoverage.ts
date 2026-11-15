import type { TimeBucket } from '../../graphql/generated/enums';
import type { OverviewIngestionCoverageQuery } from '../../graphql/generated/graphql';
import { expectedSamplesInBucket } from '../../domain/expectedSamples';
import { bucketGrid, bucketLengthMs, clipToRange, fillBuckets } from '../../domain/timeBuckets';
import { rangeDurationMs, type TimeRange } from '../../domain/timeRanges';

type CoverageRow = OverviewIngestionCoverageQuery['motionStatistics'][number];

export const COVERAGE_BUCKET: TimeBucket = 'HOUR';

/** Shorter partial hours expect so few polls that one more or less swings their share from 0 to 100 %. */
export const MIN_PARTIAL_BUCKET_MS = 60_000;

export const LOW_COVERAGE_SHARE = 0.5;

export interface CoverageHour {
  span: TimeRange;
  samples: number;
  expected: number;
  /** At most 1: polls aren't exactly 15 s apart, so a short span can get more. */
  share: number;
  full: boolean;
}

export function toIngestionCoverage(
  rows: readonly CoverageRow[],
  range: TimeRange,
  bucket: TimeBucket,
  meterCount: number,
): CoverageHour[] {
  return fillBuckets(bucketGrid(range, bucket), rows, (row) => row.bucketStart).flatMap(
    ({ start, value: row }) => {
      const span = clipToRange(start, bucket, range);
      const insideMs = rangeDurationMs(span);
      if (insideMs < MIN_PARTIAL_BUCKET_MS) {
        return [];
      }

      const samples = row?.samples ?? 0;
      const expected = expectedSamplesInBucket(start, bucket, range, meterCount);
      return [
        {
          span,
          samples,
          expected,
          share: Math.min(1, samples / expected),
          full: insideMs === bucketLengthMs(bucket),
        },
      ];
    },
  );
}

export function lowCoverageHour(hours: readonly CoverageHour[]): CoverageHour | null {
  let lowest: CoverageHour | null = null;

  for (const hour of hours) {
    if (hour.full && hour.share < LOW_COVERAGE_SHARE && (!lowest || hour.share < lowest.share)) {
      lowest = hour;
    }
  }

  return lowest;
}

export function overallShare(hours: readonly CoverageHour[]): number {
  const expected = hours.reduce((total, hour) => total + hour.expected, 0);
  const samples = hours.reduce((total, hour) => total + hour.samples, 0);
  return expected > 0 ? Math.min(1, samples / expected) : 0;
}
