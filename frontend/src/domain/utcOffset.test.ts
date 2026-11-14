import { describe, expect, it, vi } from 'vitest';

import { formatOffset, formatUtcOffset, getUtcOffsetMinutes, toIsoWithOffset } from './utcOffset';

describe('formatUtcOffset', () => {
  it.each([
    [180, 'UTC+03:00'],
    [-300, 'UTC-05:00'],
    [0, 'UTC+00:00'],
    [330, 'UTC+05:30'],
    [-210, 'UTC-03:30'],
    [345, 'UTC+05:45'],
    [840, 'UTC+14:00'],
    [-720, 'UTC-12:00'],
  ])('formats %i minutes as %s', (offsetMinutes, expected) => {
    expect(formatUtcOffset(offsetMinutes)).toBe(expected);
  });
});

describe('formatOffset', () => {
  it.each([
    [180, '+03:00'],
    [-300, '-05:00'],
    [0, '+00:00'],
    [330, '+05:30'],
  ])('formats %i minutes as %s', (offsetMinutes, expected) => {
    expect(formatOffset(offsetMinutes)).toBe(expected);
  });
});

describe('getUtcOffsetMinutes', () => {
  function dateWithTimezoneOffset(timezoneOffset: number): Date {
    const date = new Date('2026-11-13T12:00:00Z');
    date.getTimezoneOffset = () => timezoneOffset;
    return date;
  }

  it('is positive east of UTC', () => {
    expect(getUtcOffsetMinutes(dateWithTimezoneOffset(-180))).toBe(180);
  });

  it('is negative west of UTC', () => {
    expect(getUtcOffsetMinutes(dateWithTimezoneOffset(300))).toBe(-300);
  });

  it('is zero, not negative zero, at UTC', () => {
    expect(Object.is(getUtcOffsetMinutes(dateWithTimezoneOffset(0)), 0)).toBe(true);
  });
});

describe('toIsoWithOffset', () => {
  it.each([
    ['Europe/Moscow', '2026-11-14T12:00:00Z', '2026-11-14T15:00:00.000+03:00'],
    ['America/New_York', '2026-11-14T02:30:15.250Z', '2026-11-13T21:30:15.250-05:00'],
    ['Asia/Kolkata', '2026-11-14T20:00:00Z', '2026-11-15T01:30:00.000+05:30'],
    ['UTC', '2026-11-14T12:00:00Z', '2026-11-14T12:00:00.000+00:00'],
  ])('writes local time with the offset in %s', (timeZone, instant, expected) => {
    vi.stubEnv('TZ', timeZone);

    const iso = toIsoWithOffset(new Date(instant));

    expect(iso).toBe(expected);
    expect(new Date(iso).toISOString()).toBe(new Date(instant).toISOString());
  });

  it("uses each date's own offset across a daylight saving time change", () => {
    vi.stubEnv('TZ', 'Europe/Berlin');

    expect(toIsoWithOffset(new Date('2026-10-24T12:00:00Z'))).toBe('2026-10-24T14:00:00.000+02:00');
    expect(toIsoWithOffset(new Date('2026-10-26T12:00:00Z'))).toBe('2026-10-26T13:00:00.000+01:00');
  });
});
