import { describe, expect, it } from 'vitest';

import { formatUtcOffset, getUtcOffsetMinutes } from './utcOffset';

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
