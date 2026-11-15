import { meterLocations } from '../../domain/meters';
import { autoBucket, isBucketAllowed, timeBuckets } from '../../domain/timeBuckets';
import {
  checkRange,
  isRangePreset,
  MAX_STATISTICS_RANGE_MS,
  presetRange,
  type RangePreset,
  type TimeRange,
} from '../../domain/timeRanges';
import { toIsoWithOffset } from '../../domain/utcOffset';
import type { MeterLocation, TimeBucket } from '../../graphql/generated/enums';

/** A preset ends at the time the filter was applied; a custom range is fixed. */
export type RangeSelection =
  { kind: 'preset'; preset: RangePreset } | { kind: 'custom'; range: TimeRange };

export type BucketSelection = TimeBucket | 'auto';

export interface AnalyticsFilter {
  range: RangeSelection;
  bucket: BucketSelection;
  /** `null` for all locations. */
  location: MeterLocation | null;
}

export const defaultAnalyticsFilter: AnalyticsFilter = {
  range: { kind: 'preset', preset: '24h' },
  bucket: 'auto',
  location: null,
};

// A preset's length doesn't depend on its end, so any end will do for checks of the length.
const ANY_END = new Date(0);

export function resolveRange(selection: RangeSelection, now: Date): TimeRange {
  return selection.kind === 'preset' ? presetRange(selection.preset, now) : selection.range;
}

export function effectiveBucket(bucket: BucketSelection, range: TimeRange): TimeBucket {
  return bucket === 'auto' ? autoBucket(range) : bucket;
}

/** A size the range doesn't allow becomes Auto; the bucket select can't offer it either. */
function fitBucket(bucket: BucketSelection, selection: RangeSelection): BucketSelection {
  return bucket === 'auto' || isBucketAllowed(bucket, resolveRange(selection, ANY_END))
    ? bucket
    : 'auto';
}

export function withRange(filter: AnalyticsFilter, range: RangeSelection): AnalyticsFilter {
  return { ...filter, range, bucket: fitBucket(filter.bucket, range) };
}

function parseRange(params: URLSearchParams): RangeSelection {
  const value = params.get('range');

  if (value !== null && isRangePreset(value)) {
    return { kind: 'preset', preset: value };
  }

  if (value === 'custom') {
    const range = {
      from: new Date(params.get('from') ?? ''),
      to: new Date(params.get('to') ?? ''),
    };
    if (checkRange(range, MAX_STATISTICS_RANGE_MS) === null) {
      return { kind: 'custom', range };
    }
  }

  return defaultAnalyticsFilter.range;
}

/**
 * Reads the filter from the URL (`?range=24h&bucket=auto&location=KITCHEN`). Missing or invalid values
 * fall back to their defaults, so a hand-edited or outdated link still opens the page.
 */
export function parseAnalyticsFilter(params: URLSearchParams): AnalyticsFilter {
  const range = parseRange(params);
  const bucket = timeBuckets.find((size) => size === params.get('bucket')) ?? 'auto';
  const location = meterLocations.find((value) => value === params.get('location')) ?? null;

  return { range, bucket: fitBucket(bucket, range), location };
}

export function toSearchParams(filter: AnalyticsFilter): URLSearchParams {
  const params = new URLSearchParams();

  if (filter.range.kind === 'preset') {
    params.set('range', filter.range.preset);
  } else {
    params.set('range', 'custom');
    params.set('from', filter.range.range.from.toISOString());
    params.set('to', filter.range.range.to.toISOString());
  }
  params.set('bucket', filter.bucket);
  if (filter.location) {
    params.set('location', filter.location);
  }

  return params;
}

/** The statistics filter's variables; an empty `locations` list includes all locations. */
export function statisticsVariables(range: TimeRange, location: MeterLocation | null) {
  return {
    from: toIsoWithOffset(range.from),
    to: toIsoWithOffset(range.to),
    locations: location ? [location] : [],
  };
}
