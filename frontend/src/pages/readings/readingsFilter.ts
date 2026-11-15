import { toDateTimeLocalValue } from '../../domain/formatting';
import { meterLocations, meterTypes } from '../../domain/meters';
import { checkRange, type TimeRange } from '../../domain/timeRanges';
import { toIsoWithOffset } from '../../domain/utcOffset';
import type { MeterLocation, MeterType } from '../../graphql/generated/enums';

const MINUTE_MS = 60_000;
const DEFAULT_RANGE_MS = 60 * MINUTE_MS;

export interface ReadingsFilter {
  /** At least one, in `meterTypes` order, so the same selection always gives the same variables. */
  types: readonly MeterType[];
  /** `null` for all locations. */
  location: MeterLocation | null;
  range: TimeRange;
}

/**
 * All meters, the last hour. The range ends at the next whole minute, as the form's inputs show minutes:
 * what the inputs show is exactly what is applied.
 */
export function defaultReadingsFilter(now: Date): ReadingsFilter {
  const to = new Date(Math.ceil(now.getTime() / MINUTE_MS) * MINUTE_MS);
  return {
    types: meterTypes,
    location: null,
    range: { from: new Date(to.getTime() - DEFAULT_RANGE_MS), to },
  };
}

/** The selected types in `meterTypes` order, without duplicates. */
export function orderTypes(types: Iterable<string>): MeterType[] {
  const selected = new Set(types);
  return meterTypes.filter((type) => selected.has(type));
}

/** A range needs a valid start before its end; readings have no length limit. */
export function isValidRange(range: TimeRange): boolean {
  return checkRange(range, Number.POSITIVE_INFINITY) === null;
}

/**
 * Reads the filter from the URL (`?from=…&to=…&type=ENERGY&type=MOTION&location=KITCHEN`). Missing or
 * invalid values fall back to their defaults, so a hand-edited or outdated link still opens the page.
 * `now` is the time the page opened; it only matters for the default range.
 */
export function parseReadingsFilter(params: URLSearchParams, now: Date): ReadingsFilter {
  const defaults = defaultReadingsFilter(now);
  const types = orderTypes(params.getAll('type'));
  const location = meterLocations.find((value) => value === params.get('location')) ?? null;
  const range = { from: new Date(params.get('from') ?? ''), to: new Date(params.get('to') ?? '') };

  return {
    types: types.length > 0 ? types : defaults.types,
    location,
    range: isValidRange(range) ? range : defaults.range,
  };
}

/** The URL of a filter; `type` is left out for all types and `location` for all locations. */
export function toSearchParams(filter: ReadingsFilter): URLSearchParams {
  const params = new URLSearchParams();

  params.set('from', filter.range.from.toISOString());
  params.set('to', filter.range.to.toISOString());
  if (filter.types.length < meterTypes.length) {
    for (const type of filter.types) {
      params.append('type', type);
    }
  }
  if (filter.location) {
    params.set('location', filter.location);
  }

  return params;
}

/** The `ReadingFilterInput` of a filter; an empty `locations` list includes all locations. */
export function readingFilterInput(filter: ReadingsFilter) {
  return {
    types: [...filter.types],
    locations: filter.location ? [filter.location] : [],
    from: toIsoWithOffset(filter.range.from),
    to: toIsoWithOffset(filter.range.to),
  };
}

/** The form's values: `datetime-local` strings, read as local time. */
export interface ReadingsDraft {
  types: readonly MeterType[];
  location: MeterLocation | null;
  from: string;
  to: string;
}

export type DraftProblems = Partial<Record<'types' | 'from' | 'to', string>>;

export function toDraft(filter: ReadingsFilter): ReadingsDraft {
  return {
    types: filter.types,
    location: filter.location,
    from: toDateTimeLocalValue(filter.range.from),
    to: toDateTimeLocalValue(filter.range.to),
  };
}

function rangeProblems(from: string, to: string): DraftProblems {
  if (!from || !to) {
    return { [from ? 'to' : 'from']: 'Enter a start and an end.' };
  }

  return isValidRange({ from: new Date(from), to: new Date(to) })
    ? {}
    : { to: 'The end must be later than the start.' };
}

/**
 * Checks the form before anything is sent, so the gateway's rules hold: at least one type (an empty
 * list would include all of them) and a start before the end.
 */
export function readDraft(
  draft: ReadingsDraft,
): { filter: ReadingsFilter; problems: null } | { filter: null; problems: DraftProblems } {
  const problems: DraftProblems = {
    ...(draft.types.length === 0 ? { types: 'Pick at least one meter type.' } : {}),
    ...rangeProblems(draft.from, draft.to),
  };

  if (Object.keys(problems).length > 0) {
    return { filter: null, problems };
  }

  return {
    filter: {
      types: orderTypes(draft.types),
      location: draft.location,
      range: { from: new Date(draft.from), to: new Date(draft.to) },
    },
    problems: null,
  };
}
