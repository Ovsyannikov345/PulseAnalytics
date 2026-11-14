import { describe, expect, it, vi } from 'vitest';

import type { TimeBucket } from '../graphql/generated/enums';
import {
  MAX_AUTO_BUCKETS,
  MAX_BUCKETS,
  autoBucket,
  bucketGrid,
  bucketLengthMs,
  bucketStartOf,
  clipToRange,
  fillBuckets,
  isBucketAllowed,
  maxRangeMs,
  timeBucketLabel,
  timeBuckets,
} from './timeBuckets';
import { presetRange, type TimeRange } from './timeRanges';

const MINUTE_MS = 60_000;
const HOUR_MS = 60 * MINUTE_MS;
const DAY_MS = 24 * HOUR_MS;

function range(from: string, to: string): TimeRange {
  return { from: new Date(from), to: new Date(to) };
}

function rangeOf(durationMs: number): TimeRange {
  const from = new Date('2026-11-01T00:00:00Z');
  return { from, to: new Date(from.getTime() + durationMs) };
}

function isoStrings(dates: Date[]): string[] {
  return dates.map((date) => date.toISOString());
}

describe('time buckets', () => {
  it('lists the sizes from the finest to the coarsest with their lengths and labels', () => {
    expect(timeBuckets).toEqual(['MINUTE', 'FIVE_MINUTES', 'FIFTEEN_MINUTES', 'HOUR', 'DAY']);
    expect(timeBuckets.map(bucketLengthMs)).toEqual([
      MINUTE_MS,
      5 * MINUTE_MS,
      15 * MINUTE_MS,
      HOUR_MS,
      DAY_MS,
    ]);
    expect(timeBuckets.map(timeBucketLabel)).toEqual([
      '1 minute',
      '5 minutes',
      '15 minutes',
      '1 hour',
      '1 day',
    ]);
  });
});

describe('isBucketAllowed', () => {
  it.each(timeBuckets)('allows %s up to exactly 1,000 bucket lengths', (bucket) => {
    const limit = MAX_BUCKETS * bucketLengthMs(bucket);

    expect(maxRangeMs(bucket)).toBe(limit);
    expect(isBucketAllowed(bucket, rangeOf(limit))).toBe(true);
    expect(isBucketAllowed(bucket, rangeOf(limit + 1))).toBe(false);
  });

  it.each([
    ['24h', ['MINUTE']],
    ['7d', ['MINUTE', 'FIVE_MINUTES']],
    ['31d', ['MINUTE', 'FIVE_MINUTES', 'FIFTEEN_MINUTES']],
  ] as const)('disables only %s too fine for the %s preset', (preset, disabled) => {
    const presetRangeNow = presetRange(preset, new Date('2026-11-14T12:00:00Z'));

    const notAllowed = timeBuckets.filter((bucket) => !isBucketAllowed(bucket, presetRangeNow));

    expect(notAllowed).toEqual(disabled);
  });
});

describe('autoBucket', () => {
  it.each([
    ['1h', 'MINUTE'],
    ['6h', 'FIVE_MINUTES'],
    ['24h', 'FIFTEEN_MINUTES'],
    ['7d', 'HOUR'],
    ['31d', 'DAY'],
  ] as const)('picks %s → %s', (preset, expected) => {
    expect(autoBucket(presetRange(preset, new Date('2026-11-14T12:34:56Z')))).toBe(expected);
  });

  it.each(['MINUTE', 'FIVE_MINUTES', 'FIFTEEN_MINUTES', 'HOUR'] as const)(
    'keeps %s up to exactly 200 bucket lengths and moves on after',
    (bucket) => {
      const limit = MAX_AUTO_BUCKETS * bucketLengthMs(bucket);
      const next = timeBuckets[timeBuckets.indexOf(bucket) + 1];

      expect(autoBucket(rangeOf(limit))).toBe(bucket);
      expect(autoBucket(rangeOf(limit + 1))).toBe(next);
    },
  );

  it('falls back to DAY for ranges too long for 200 days', () => {
    expect(autoBucket(rangeOf(400 * DAY_MS))).toBe('DAY');
  });
});

describe('bucketStartOf', () => {
  it.each([
    ['MINUTE', '2026-11-14T12:34:56.789Z', '2026-11-14T12:34:00.000Z'],
    ['FIVE_MINUTES', '2026-11-14T12:34:56Z', '2026-11-14T12:30:00.000Z'],
    ['FIFTEEN_MINUTES', '2026-11-14T12:44:59.999Z', '2026-11-14T12:30:00.000Z'],
    ['HOUR', '2026-11-14T12:00:00Z', '2026-11-14T12:00:00.000Z'],
    ['DAY', '2026-11-14T23:59:59Z', '2026-11-14T00:00:00.000Z'],
    ['DAY', '1969-12-31T12:00:00Z', '1969-12-31T00:00:00.000Z'],
  ] as const)('puts %s buckets on the UTC grid: %s → %s', (bucket, date, expected) => {
    expect(bucketStartOf(new Date(date), bucket).toISOString()).toBe(expected);
  });
});

describe('bucketGrid', () => {
  it('covers an aligned range without a bucket at the exclusive end', () => {
    const grid = bucketGrid(range('2026-11-14T10:00:00Z', '2026-11-14T13:00:00Z'), 'HOUR');

    expect(isoStrings(grid)).toEqual([
      '2026-11-14T10:00:00.000Z',
      '2026-11-14T11:00:00.000Z',
      '2026-11-14T12:00:00.000Z',
    ]);
  });

  it('includes the partial buckets at both ends of an unaligned range', () => {
    const grid = bucketGrid(range('2026-11-14T10:20:00Z', '2026-11-14T12:00:01Z'), 'HOUR');

    expect(isoStrings(grid)).toEqual([
      '2026-11-14T10:00:00.000Z',
      '2026-11-14T11:00:00.000Z',
      '2026-11-14T12:00:00.000Z',
    ]);
  });

  it('stays on the grid instead of starting at from', () => {
    const grid = bucketGrid(range('2026-11-14T10:02:00Z', '2026-11-14T10:16:00Z'), 'FIVE_MINUTES');

    expect(isoStrings(grid)).toEqual([
      '2026-11-14T10:00:00.000Z',
      '2026-11-14T10:05:00.000Z',
      '2026-11-14T10:10:00.000Z',
      '2026-11-14T10:15:00.000Z',
    ]);
  });

  it('gives one bucket per 15 minutes for the 24 h preset, plus the partial one', () => {
    const grid = bucketGrid(
      presetRange('24h', new Date('2026-11-14T11:29:47Z')),
      'FIFTEEN_MINUTES',
    );

    expect(grid).toHaveLength(97);
    expect(grid[0]?.toISOString()).toBe('2026-11-13T11:15:00.000Z');
    expect(grid.at(-1)?.toISOString()).toBe('2026-11-14T11:15:00.000Z');
  });

  it('is empty for an empty range', () => {
    expect(bucketGrid(range('2026-11-14T10:00:00Z', '2026-11-14T10:00:00Z'), 'HOUR')).toEqual([]);
  });

  describe('across a daylight saving time change (Europe/Berlin, 2026-10-25)', () => {
    it.each([
      ['DAY', '2026-10-23T12:00:00Z', '2026-10-27T12:00:00Z', 5],
      ['HOUR', '2026-10-24T22:00:00Z', '2026-10-25T04:00:00Z', 6],
    ] as const)('keeps %s buckets on UTC multiples', (bucket, from, to, count) => {
      vi.stubEnv('TZ', 'Europe/Berlin');
      // The local offset really changes inside the range: UTC+02:00, then UTC+01:00.
      expect(new Date(from).getTimezoneOffset()).toBe(-120);
      expect(new Date(to).getTimezoneOffset()).toBe(-60);

      const grid = bucketGrid(range(from, to), bucket);

      expect(grid).toHaveLength(count);
      for (const start of grid) {
        expect(start.getTime() % bucketLengthMs(bucket)).toBe(0);
      }
    });
  });
});

describe('fillBuckets', () => {
  const grid = bucketGrid(range('2026-11-14T10:00:00Z', '2026-11-14T14:00:00Z'), 'HOUR');

  interface Row {
    bucketStart: string | null;
    samples: number;
  }

  it('matches rows to their buckets and leaves gaps where the gateway returned none', () => {
    const rows: Row[] = [
      { bucketStart: '2026-11-14T10:00:00Z', samples: 240 },
      // Another notation of the same instant still matches.
      { bucketStart: '2026-11-14T15:00:00.000+03:00', samples: 120 },
    ];

    const slots = fillBuckets(grid, rows, (row) => row.bucketStart);

    expect(slots.map((slot) => [slot.start.toISOString(), slot.value?.samples ?? null])).toEqual([
      ['2026-11-14T10:00:00.000Z', 240],
      ['2026-11-14T11:00:00.000Z', null],
      ['2026-11-14T12:00:00.000Z', 120],
      ['2026-11-14T13:00:00.000Z', null],
    ]);
  });

  it('gives only gaps for an empty result', () => {
    const slots = fillBuckets(grid, [] as Row[], (row) => row.bucketStart);

    expect(slots).toHaveLength(4);
    expect(slots.every((slot) => slot.value === null)).toBe(true);
  });

  it('drops rows without a bucket start or outside the grid', () => {
    const rows: Row[] = [
      { bucketStart: null, samples: 1 },
      { bucketStart: '2026-11-14T09:00:00Z', samples: 2 },
      { bucketStart: '2026-11-14T11:30:00Z', samples: 3 },
    ];

    const slots = fillBuckets(grid, rows, (row) => row.bucketStart);

    expect(slots.every((slot) => slot.value === null)).toBe(true);
  });
});

describe('clipToRange', () => {
  const window = range('2026-11-14T10:20:00Z', '2026-11-14T12:40:00Z');

  it.each([
    ['the first, partial', '2026-11-14T10:00:00Z', '2026-11-14T10:20:00Z', '2026-11-14T11:00:00Z'],
    ['a middle, whole', '2026-11-14T11:00:00Z', '2026-11-14T11:00:00Z', '2026-11-14T12:00:00Z'],
    ['the last, partial', '2026-11-14T12:00:00Z', '2026-11-14T12:00:00Z', '2026-11-14T12:40:00Z'],
  ])('keeps the part of %s bucket inside the range', (_, start, from, to) => {
    const clipped = clipToRange(new Date(start), 'HOUR' satisfies TimeBucket, window);

    expect(clipped).toEqual(range(from, to));
  });

  it('is empty for a bucket outside the range', () => {
    const clipped = clipToRange(new Date('2026-11-14T13:00:00Z'), 'HOUR', window);

    expect(clipped.to.getTime() - clipped.from.getTime()).toBe(0);
  });
});
