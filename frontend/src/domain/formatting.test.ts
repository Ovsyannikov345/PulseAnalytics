import { beforeEach, describe, expect, it, vi } from 'vitest';

import {
  formatBucketRange,
  formatBucketStart,
  formatDateTime,
  formatDateTimeWithSeconds,
  formatDayMonth,
  formatDayStartHint,
  formatDays,
  formatElapsed,
  formatHourMinute,
  formatNumber,
  formatPercent,
  formatRangeLimit,
  formatTime,
  toDateTimeLocalValue,
} from './formatting';

const HOUR_MS = 3_600_000;
const DAY_MS = 24 * HOUR_MS;

describe('formatNumber', () => {
  it.each([
    [3012.5249, 2, '3,012.52'],
    [79836, 0, '79,836'],
    [671.6, 0, '672'],
    [1234567.891, 1, '1,234,567.9'],
    [0, 2, '0.00'],
    [19, undefined, '19'],
  ])('formats %d with %s decimals as %s', (value, fractionDigits, expected) => {
    expect(formatNumber(value, fractionDigits)).toBe(expected);
  });
});

describe('formatPercent', () => {
  it.each([
    [0.77, undefined, '77 %'],
    [0.7701, 1, '77.0 %'],
    [1, undefined, '100 %'],
    [0, undefined, '0 %'],
  ])('formats %d as %s', (ratio, fractionDigits, expected) => {
    expect(formatPercent(ratio, fractionDigits)).toBe(expected);
  });
});

describe('formatElapsed', () => {
  it.each([
    [0, '0 min'],
    [119_999, '1 min'],
    [120_000, '2 min'],
    [59 * 60_000 + 59_999, '59 min'],
    [60 * 60_000, '1 h'],
    [65 * 60_000 + 30_000, '1 h 5 min'],
    [26 * 60 * 60_000, '26 h'],
    [-5_000, '0 min'],
  ])('formats %d ms as %s', (milliseconds, expected) => {
    expect(formatElapsed(milliseconds)).toBe(expected);
  });
});

describe('date and time formatting', () => {
  const instant = new Date('2026-11-10T11:30:05Z');

  describe('at UTC+03:00', () => {
    beforeEach(() => {
      vi.stubEnv('TZ', 'Europe/Moscow');
    });

    it('formats local times with and without seconds', () => {
      expect(formatTime(instant)).toBe('14:30:05');
      expect(formatHourMinute(instant)).toBe('14:30');
    });

    it('uses the 24-hour clock with a leading zero', () => {
      const earlyMorning = new Date('2026-11-09T21:05:09Z');

      expect(formatTime(earlyMorning)).toBe('00:05:09');
      expect(formatHourMinute(earlyMorning)).toBe('00:05');
    });

    it('formats the local date when it is already the next day in UTC terms', () => {
      // 9 Nov 21:05 UTC is 10 Nov 00:05 in Moscow.
      expect(formatDateTime(new Date('2026-11-09T21:05:09Z'))).toBe('10 Nov 00:05');
    });

    it('formats the local date and time', () => {
      expect(formatDayMonth(instant)).toBe('10 Nov');
      expect(formatDateTime(instant)).toBe('10 Nov 14:30');
      expect(formatDateTimeWithSeconds(instant)).toBe('10 Nov 14:30:05');
    });

    it('labels sub-day buckets with the local time and DAY buckets with the date', () => {
      expect(formatBucketStart(new Date('2026-11-10T11:15:00Z'), 'FIFTEEN_MINUTES')).toBe('14:15');
      expect(formatBucketStart(new Date('2026-11-10T00:00:00Z'), 'DAY')).toBe('10 Nov');
    });
  });

  describe('at UTC-05:00', () => {
    beforeEach(() => {
      vi.stubEnv('TZ', 'America/New_York');
    });

    it('formats the local date, which can differ from the UTC date', () => {
      const lateEvening = new Date('2026-11-10T02:00:00Z');

      expect(formatDateTime(lateEvening)).toBe('9 Nov 21:00');
    });

    it('labels a DAY bucket with its UTC date, not the local date at its start', () => {
      // The bucket for 10 Nov starts at 19:00 on 9 Nov local time.
      expect(formatBucketStart(new Date('2026-11-10T00:00:00Z'), 'DAY')).toBe('10 Nov');
    });
  });

  describe('at UTC+05:30', () => {
    beforeEach(() => {
      vi.stubEnv('TZ', 'Asia/Kolkata');
    });

    it('formats half-hour offsets', () => {
      expect(formatTime(instant)).toBe('17:00:05');
      expect(formatBucketStart(new Date('2026-11-10T11:15:00Z'), 'HOUR')).toBe('16:45');
    });
  });
});

describe('formatDayStartHint', () => {
  it.each([
    [180, 'Days start at 03:00 local time (00:00 UTC)'],
    [330, 'Days start at 05:30 local time (00:00 UTC)'],
    [-300, 'Days start at 19:00 local time (00:00 UTC)'],
    [-210, 'Days start at 20:30 local time (00:00 UTC)'],
    [840, 'Days start at 14:00 local time (00:00 UTC)'],
    [-720, 'Days start at 12:00 local time (00:00 UTC)'],
  ])('describes an offset of %i minutes', (offsetMinutes, expected) => {
    expect(formatDayStartHint(offsetMinutes)).toBe(expected);
  });

  it('gives no hint at UTC', () => {
    expect(formatDayStartHint(0)).toBeNull();
  });
});

describe('formatBucketRange', () => {
  beforeEach(() => {
    vi.stubEnv('TZ', 'Europe/Moscow');
  });

  it.each([
    ['2026-11-15T05:45:00Z', 'FIFTEEN_MINUTES', '15 Nov 08:45–09:00'],
    ['2026-11-15T20:00:00Z', 'HOUR', '15 Nov 23:00–00:00'],
    ['2026-11-15T11:29:00Z', 'MINUTE', '15 Nov 14:29–14:30'],
    ['2026-11-15T00:00:00Z', 'DAY', '15 Nov'],
  ] as const)('formats a bucket starting %s (%s) as %s', (start, bucket, expected) => {
    expect(formatBucketRange(new Date(start), bucket)).toBe(expected);
  });
});

describe('formatRangeLimit', () => {
  it.each([
    [1000 * 60_000, '16 h'],
    [47 * HOUR_MS + 59 * 60_000, '47 h'],
    [48 * HOUR_MS, '2 d'],
    [5000 * 60_000, '3 d'],
    [15_000 * 60_000, '10 d'],
  ])('formats %d ms as %s', (milliseconds, expected) => {
    expect(formatRangeLimit(milliseconds)).toBe(expected);
  });
});

describe('formatDays', () => {
  it.each([
    [DAY_MS, '1 day'],
    [40 * DAY_MS, '40 days'],
    [31 * DAY_MS + 60_000, '31.1 days'],
    [12 * HOUR_MS, '0.5 days'],
  ])('formats %d ms as %s', (milliseconds, expected) => {
    expect(formatDays(milliseconds)).toBe(expected);
  });
});

describe('toDateTimeLocalValue', () => {
  it('gives the local date and time to the minute, with leading zeros', () => {
    vi.stubEnv('TZ', 'Europe/Moscow');

    expect(toDateTimeLocalValue(new Date('2026-01-04T06:05:59Z'))).toBe('2026-01-04T09:05');
  });

  it('reads back as the same local minute', () => {
    vi.stubEnv('TZ', 'America/New_York');
    const value = toDateTimeLocalValue(new Date('2026-11-10T02:00:00Z'));

    expect(value).toBe('2026-11-09T21:00');
    expect(new Date(value).toISOString()).toBe('2026-11-10T02:00:00.000Z');
  });
});
