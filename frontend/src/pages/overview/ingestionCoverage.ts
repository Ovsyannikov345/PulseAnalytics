import type { TimeBucket } from '../../graphql/generated/enums';
import type { OverviewIngestionCoverageQuery } from '../../graphql/generated/graphql';
import { expectedSamplesInBucket } from '../../domain/expectedSamples';
import { bucketGrid, bucketLengthMs, clipToRange, fillBuckets } from '../../domain/timeBuckets';
import { rangeDurationMs, type TimeRange } from '../../domain/timeRanges';

type CoverageRow = OverviewIngestionCoverageQuery['motionStatistics'][number];

/** The coverage chart: stored samples per hour over the last 24 hours. */
export const COVERAGE_BUCKET: TimeBucket = 'HOUR';

/**
 * A partial hour at either end of the range with less than this inside it is left out: it expects
 * only a few polls, so one poll more or less would swing its share between 0 and 100 %.
 */
export const MIN_PARTIAL_BUCKET_MS = 60_000;

/** A full hour below this share is called out under the chart. */
export const LOW_COVERAGE_SHARE = 0.5;

export interface CoverageHour {
  /** The part of the hour inside the range: the whole hour except possibly at the range's ends. */
  span: TimeRange;
  samples: number;
  /** The samples the part of the hour inside the range would hold if every poll succeeded. */
  expected: number;
  /** `samples / expected`, at most 1: polls aren't exactly 15 s apart, so a short span can get more. */
  share: number;
  /** Whether the whole hour lies inside the range. */
  full: boolean;
}

/**
 * The share of expected samples per hour. The coverage is measured on one meter type (each poll
 * stores a reading of every meter), so `meterCount` is the number of meters of that type.
 */
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

/** The full hour with the lowest share, if it is below `LOW_COVERAGE_SHARE`. */
export function lowCoverageHour(hours: readonly CoverageHour[]): CoverageHour | null {
  let lowest: CoverageHour | null = null;

  for (const hour of hours) {
    if (hour.full && hour.share < LOW_COVERAGE_SHARE && (!lowest || hour.share < lowest.share)) {
      lowest = hour;
    }
  }

  return lowest;
}

/** Stored ÷ expected samples over all hours. */
export function overallShare(hours: readonly CoverageHour[]): number {
  const expected = hours.reduce((total, hour) => total + hour.expected, 0);
  const samples = hours.reduce((total, hour) => total + hour.samples, 0);
  return expected > 0 ? Math.min(1, samples / expected) : 0;
}
