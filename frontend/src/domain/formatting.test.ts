import { beforeEach, describe, expect, it, vi } from 'vitest';

import {
  formatBucketStart,
  formatDateTime,
  formatDayMonth,
  formatDayStartHint,
  formatHourMinute,
  formatNumber,
  formatPercent,
  formatTime,
} from './formatting';

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

    it('formats the local date and time', () => {
      expect(formatDayMonth(instant)).toBe('10 Nov');
      expect(formatDateTime(instant)).toBe('10 Nov 14:30');
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
