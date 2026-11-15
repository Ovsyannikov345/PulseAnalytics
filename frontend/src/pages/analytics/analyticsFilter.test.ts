import { describe, expect, it, vi } from 'vitest';

import {
  defaultAnalyticsFilter,
  effectiveBucket,
  parseAnalyticsFilter,
  resolveRange,
  statisticsVariables,
  toSearchParams,
  withRange,
  type AnalyticsFilter,
} from './analyticsFilter';

const HOUR_MS = 3_600_000;
const DAY_MS = 24 * HOUR_MS;

const now = new Date('2026-11-15T11:30:00Z');
const customRange = {
  from: new Date('2026-11-01T06:00:00Z'),
  to: new Date('2026-11-08T06:00:00Z'),
};

function parse(search: string): AnalyticsFilter {
  return parseAnalyticsFilter(new URLSearchParams(search));
}

describe('parseAnalyticsFilter', () => {
  it('defaults to the last 24 hours, Auto and all locations', () => {
    expect(parse('')).toEqual(defaultAnalyticsFilter);
    expect(defaultAnalyticsFilter).toEqual({
      range: { kind: 'preset', preset: '24h' },
      bucket: 'auto',
      location: null,
    });
  });

  it('reads a preset, a bucket size and a location', () => {
    expect(parse('range=7d&bucket=HOUR&location=LIVING_ROOM')).toEqual({
      range: { kind: 'preset', preset: '7d' },
      bucket: 'HOUR',
      location: 'LIVING_ROOM',
    });
  });

  it('reads the location alone, as the Overview links to it', () => {
    expect(parse('location=KITCHEN')).toEqual({ ...defaultAnalyticsFilter, location: 'KITCHEN' });
  });

  it('reads a custom range', () => {
    expect(parse('range=custom&from=2026-11-01T06:00:00.000Z&to=2026-11-08T06:00:00.000Z')).toEqual(
      { ...defaultAnalyticsFilter, range: { kind: 'custom', range: customRange } },
    );
  });

  it.each([
    ['an unknown preset', 'range=2h'],
    ['a custom range without a start', 'range=custom&to=2026-11-01T06:00:00Z'],
    ['a custom range without an end', 'range=custom&from=2026-11-01T06:00:00Z'],
    ['an unparseable custom range', 'range=custom&from=yesterday&to=today'],
    ['a custom range that ends before it starts', 'range=custom&from=2026-11-08&to=2026-11-01'],
    ['a custom range over 31 days', 'range=custom&from=2026-10-01&to=2026-11-02'],
  ])('falls back to 24 h for %s', (_, search) => {
    expect(parse(search).range).toEqual({ kind: 'preset', preset: '24h' });
  });

  it.each([
    ['an unknown bucket', 'bucket=WEEK'],
    ['a lower-case size', 'bucket=hour'],
    ['a size the range does not allow', 'range=31d&bucket=MINUTE'],
  ])('falls back to Auto for %s', (_, search) => {
    expect(parse(search).bucket).toBe('auto');
  });

  it('keeps a size the range allows at its exact limit', () => {
    // FIFTEEN_MINUTES allows 1,000 × 15 min ≈ 10.4 days, so 7 days fit and 31 days don't.
    expect(parse('range=7d&bucket=FIFTEEN_MINUTES').bucket).toBe('FIFTEEN_MINUTES');
    expect(parse('range=31d&bucket=FIFTEEN_MINUTES').bucket).toBe('auto');
  });

  it('ignores an unknown location', () => {
    expect(parse('location=ATTIC').location).toBeNull();
  });
});

describe('toSearchParams', () => {
  it.each<[string, AnalyticsFilter]>([
    ['the default', defaultAnalyticsFilter],
    ['a preset with a size and a location', parse('range=6h&bucket=FIVE_MINUTES&location=GARAGE')],
    [
      'a custom range',
      { ...defaultAnalyticsFilter, range: { kind: 'custom', range: customRange } },
    ],
  ])('round-trips %s through the URL', (_, filter) => {
    expect(parseAnalyticsFilter(toSearchParams(filter))).toEqual(filter);
  });

  it('writes a preset, the bucket and the location', () => {
    expect(
      toSearchParams({ ...defaultAnalyticsFilter, bucket: 'HOUR', location: 'KITCHEN' }).toString(),
    ).toBe('range=24h&bucket=HOUR&location=KITCHEN');
  });

  it('writes a custom range as UTC instants and leaves out all locations', () => {
    const params = toSearchParams({
      ...defaultAnalyticsFilter,
      range: { kind: 'custom', range: customRange },
    });

    expect(params.toString()).toBe(
      'range=custom&from=2026-11-01T06%3A00%3A00.000Z&to=2026-11-08T06%3A00%3A00.000Z&bucket=auto',
    );
  });
});

describe('withRange', () => {
  it('keeps a size the new range allows', () => {
    const filter = { ...defaultAnalyticsFilter, bucket: 'HOUR' as const };

    expect(withRange(filter, { kind: 'preset', preset: '31d' })).toEqual({
      ...filter,
      range: { kind: 'preset', preset: '31d' },
    });
  });

  it('resets a size the new range does not allow to Auto', () => {
    const filter = {
      ...defaultAnalyticsFilter,
      bucket: 'MINUTE' as const,
      location: 'OFFICE' as const,
    };

    expect(withRange(filter, { kind: 'preset', preset: '7d' })).toEqual({
      range: { kind: 'preset', preset: '7d' },
      bucket: 'auto',
      location: 'OFFICE',
    });
  });

  it('checks a custom range by its length', () => {
    const filter = { ...defaultAnalyticsFilter, bucket: 'MINUTE' as const };
    const short = {
      from: customRange.from,
      to: new Date(customRange.from.getTime() + 16 * HOUR_MS),
    };

    expect(withRange(filter, { kind: 'custom', range: short }).bucket).toBe('MINUTE');
    expect(withRange(filter, { kind: 'custom', range: customRange }).bucket).toBe('auto');
  });
});

describe('resolveRange', () => {
  it('ends a preset now', () => {
    expect(resolveRange({ kind: 'preset', preset: '7d' }, now)).toEqual({
      from: new Date(now.getTime() - 7 * DAY_MS),
      to: now,
    });
  });

  it('keeps a custom range', () => {
    expect(resolveRange({ kind: 'custom', range: customRange }, now)).toBe(customRange);
  });
});

describe('effectiveBucket', () => {
  const lastDay = { from: new Date(now.getTime() - DAY_MS), to: now };

  it("uses Auto's choice", () => {
    expect(effectiveBucket('auto', lastDay)).toBe('FIFTEEN_MINUTES');
  });

  it('uses a selected size', () => {
    expect(effectiveBucket('HOUR', lastDay)).toBe('HOUR');
  });
});

describe('statisticsVariables', () => {
  it('sends local times with their offset and an empty list for all locations', () => {
    vi.stubEnv('TZ', 'Europe/Moscow');

    expect(statisticsVariables(customRange, null)).toEqual({
      from: '2026-11-01T09:00:00.000+03:00',
      to: '2026-11-08T09:00:00.000+03:00',
      locations: [],
    });
  });

  it('sends one location as a list', () => {
    expect(statisticsVariables(customRange, 'BEDROOM').locations).toEqual(['BEDROOM']);
  });
});
