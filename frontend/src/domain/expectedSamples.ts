import type { TimeBucket } from '../graphql/generated/enums';
import { clipToRange } from './timeBuckets';
import { rangeDurationMs, type TimeRange } from './timeRanges';

/**
 * The ingestor polls WeakApp every 15 s and every successful poll stores one sample per meter
 * (`MeterPollingWorker:PollingPeriodInSeconds` in services/DataIngestor/DataIngestor.Api/appsettings.json).
 * The coverage figures depend on it on purpose: when that setting changes, change this one too.
 */
export const POLL_PERIOD_MS = 15_000;

/** The samples `meterCount` meters produce in the range if every poll succeeds; fractional for partial polls. */
export function expectedSamples(range: TimeRange, meterCount: number): number {
  return (meterCount * Math.max(0, rangeDurationMs(range))) / POLL_PERIOD_MS;
}

/** As `expectedSamples`, for the part of one bucket inside the range (the first and last may be partial). */
export function expectedSamplesInBucket(
  start: Date,
  bucket: TimeBucket,
  range: TimeRange,
  meterCount: number,
): number {
  return expectedSamples(clipToRange(start, bucket, range), meterCount);
}
