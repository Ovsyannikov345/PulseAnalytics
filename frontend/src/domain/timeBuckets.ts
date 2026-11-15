import type { TimeBucket } from '../graphql/generated/enums';
import { rangeDurationMs, type TimeRange } from './timeRanges';

const MINUTE_MS = 60 * 1000;

const bucketDefinitions: Record<TimeBucket, { lengthMs: number; label: string }> = {
  MINUTE: { lengthMs: MINUTE_MS, label: '1 minute' },
  FIVE_MINUTES: { lengthMs: 5 * MINUTE_MS, label: '5 minutes' },
  FIFTEEN_MINUTES: { lengthMs: 15 * MINUTE_MS, label: '15 minutes' },
  HOUR: { lengthMs: 60 * MINUTE_MS, label: '1 hour' },
  DAY: { lengthMs: 24 * 60 * MINUTE_MS, label: '1 day' },
};

/** From the finest to the coarsest. */
export const timeBuckets = Object.keys(bucketDefinitions) as readonly TimeBucket[];

/** The gateway's limit: a range may span at most this many bucket lengths (`StatisticsLimits`). */
export const MAX_BUCKETS = 1000;

/** Auto picks the finest bucket that gives at most this many buckets: enough detail for a chart. */
export const MAX_AUTO_BUCKETS = 200;

export function bucketLengthMs(bucket: TimeBucket): number {
  return bucketDefinitions[bucket].lengthMs;
}

export function timeBucketLabel(bucket: TimeBucket): string {
  return bucketDefinitions[bucket].label;
}

/** The longest range the gateway accepts with this bucket. */
export function maxRangeMs(bucket: TimeBucket): number {
  return MAX_BUCKETS * bucketLengthMs(bucket);
}

/** Whether the gateway accepts this bucket for the range; the bucket select disables the others. */
export function isBucketAllowed(bucket: TimeBucket, range: TimeRange): boolean {
  return rangeDurationMs(range) <= maxRangeMs(bucket);
}

/**
 * The finest bucket with at most 200 bucket lengths in the range:
 * 1 h → MINUTE (60), 6 h → FIVE_MINUTES (72), 24 h → FIFTEEN_MINUTES (96), 7 d → HOUR (168), 31 d → DAY (31).
 * A range that doesn't start on the grid touches one bucket more.
 */
export function autoBucket(range: TimeRange): TimeBucket {
  const durationMs = rangeDurationMs(range);
  return (
    timeBuckets.find((bucket) => durationMs <= MAX_AUTO_BUCKETS * bucketLengthMs(bucket)) ?? 'DAY'
  );
}

/**
 * The start of the bucket that contains `date`, on the gateway's fixed UTC grid
 * (`date_bin` with origin 1970-01-01T00:00Z): hours start on the hour, days at UTC midnight.
 * Epoch milliseconds have no time zone, so daylight saving time can't shift the grid.
 */
export function bucketStartOf(date: Date, bucket: TimeBucket): Date {
  const lengthMs = bucketLengthMs(bucket);
  return new Date(Math.floor(date.getTime() / lengthMs) * lengthMs);
}

/**
 * The starts of all buckets that overlap the range, oldest first. The first bucket may start before
 * `from` and the last one may end after `to`; both then cover the range only in part.
 */
export function bucketGrid(range: TimeRange, bucket: TimeBucket): Date[] {
  const lengthMs = bucketLengthMs(bucket);
  const toMs = range.to.getTime();
  const starts: Date[] = [];

  for (
    let startMs = bucketStartOf(range.from, bucket).getTime();
    startMs < toMs;
    startMs += lengthMs
  ) {
    starts.push(new Date(startMs));
  }

  return starts;
}

/** One bucket of a chart series: the gateway's row, or `null` where it returned none (a gap). */
export interface BucketSlot<T> {
  start: Date;
  value: T | null;
}

/**
 * Puts the gateway's rows on the bucket grid. The gateway leaves out buckets without readings, so those
 * become `null` and charts show a gap there. Rows are matched by their bucket start as an instant, not
 * as a string; rows without one or outside the grid are dropped.
 */
export function fillBuckets<T>(
  grid: readonly Date[],
  rows: readonly T[],
  getBucketStart: (row: T) => string | null | undefined,
): BucketSlot<T>[] {
  const rowsByStart = new Map<number, T>();

  for (const row of rows) {
    const start = getBucketStart(row);
    if (start) {
      rowsByStart.set(new Date(start).getTime(), row);
    }
  }

  return grid.map((start) => ({ start, value: rowsByStart.get(start.getTime()) ?? null }));
}

/** The part of the bucket starting at `start` that lies inside the range (empty if none does). */
export function clipToRange(start: Date, bucket: TimeBucket, range: TimeRange): TimeRange {
  const fromMs = Math.max(start.getTime(), range.from.getTime());
  const toMs = Math.min(start.getTime() + bucketLengthMs(bucket), range.to.getTime());
  return { from: new Date(fromMs), to: new Date(Math.max(fromMs, toMs)) };
}

/** All of a bucket's rows, e.g. one per location; empty where the gateway returned none. */
export interface BucketGroup<T> {
  start: Date;
  rows: T[];
}

/** As `fillBuckets`, for rows grouped by more than time: every bucket of the grid gets all its rows. */
export function groupBuckets<T>(
  grid: readonly Date[],
  rows: readonly T[],
  getBucketStart: (row: T) => string | null | undefined,
): BucketGroup<T>[] {
  const groups = grid.map((start) => ({ start, rows: [] as T[] }));
  const groupsByStart = new Map(groups.map((group) => [group.start.getTime(), group]));

  for (const row of rows) {
    const start = getBucketStart(row);
    if (start) {
      groupsByStart.get(new Date(start).getTime())?.rows.push(row);
    }
  }

  return groups;
}
