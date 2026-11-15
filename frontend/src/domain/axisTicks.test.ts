import { beforeEach, describe, expect, it, vi } from 'vitest';

import { localHourTicks, timeAxis, type AxisTick } from './axisTicks';
import { formatDateTime } from './formatting';

function labels(ticks: Date[]): string[] {
  return ticks.map(formatDateTime);
}

describe('localHourTicks', () => {
  it('starts at the first full local hour and steps on the local clock', () => {
    vi.stubEnv('TZ', 'Europe/Moscow');
    const range = { from: new Date('2026-11-14T11:30:00Z'), to: new Date('2026-11-15T11:30:00Z') };

    expect(labels(localHourTicks(range, 4))).toEqual([
      '14 Nov 15:00',
      '14 Nov 19:00',
      '14 Nov 23:00',
      '15 Nov 03:00',
      '15 Nov 07:00',
      '15 Nov 11:00',
    ]);
  });

  it('starts at `from` when it is a full hour and leaves out `to`', () => {
    vi.stubEnv('TZ', 'Europe/Moscow');
    const range = { from: new Date('2026-11-15T09:00:00Z'), to: new Date('2026-11-15T11:00:00Z') };

    expect(labels(localHourTicks(range, 1))).toEqual(['15 Nov 12:00', '15 Nov 13:00']);
  });

  it('uses local full hours where the offset has half hours', () => {
    vi.stubEnv('TZ', 'Asia/Kolkata');
    // 10:00 UTC is 15:30 in Kolkata.
    const range = { from: new Date('2026-11-15T10:00:00Z'), to: new Date('2026-11-15T13:00:00Z') };

    const ticks = localHourTicks(range, 1);

    expect(labels(ticks)).toEqual(['15 Nov 16:00', '15 Nov 17:00', '15 Nov 18:00']);
    expect(ticks[0]?.toISOString()).toBe('2026-11-15T10:30:00.000Z');
  });

  it('keeps regular labels across the end of daylight saving time', () => {
    vi.stubEnv('TZ', 'Europe/Berlin');
    // Clocks go back from 03:00 CEST to 02:00 CET on 25 Oct 2026.
    const range = { from: new Date('2026-10-24T22:00:00Z'), to: new Date('2026-10-25T09:00:00Z') };

    const ticks = localHourTicks(range, 4);

    expect(labels(ticks)).toEqual(['25 Oct 00:00', '25 Oct 04:00', '25 Oct 08:00']);
    expect((ticks[1]?.getTime() ?? 0) - (ticks[0]?.getTime() ?? 0)).toBe(5 * 3_600_000);
  });

  it('is empty when the range holds no full hour', () => {
    vi.stubEnv('TZ', 'Europe/Moscow');
    const range = { from: new Date('2026-11-15T10:10:00Z'), to: new Date('2026-11-15T10:50:00Z') };

    expect(localHourTicks(range, 1)).toEqual([]);
  });
});

describe('timeAxis', () => {
  const HOUR_MS = 3_600_000;
  const DAY_MS = 24 * HOUR_MS;
  // 14:30 in Moscow.
  const now = new Date('2026-11-15T11:30:00Z');

  function lastOf(durationMs: number) {
    return { from: new Date(now.getTime() - durationMs), to: now };
  }

  function axisLabels(ticks: AxisTick[]): string[] {
    return ticks.map((tick) => tick.label);
  }

  beforeEach(() => {
    vi.stubEnv('TZ', 'Europe/Moscow');
  });

  it('labels 24 hours every 4 local hours and ends at `to`, as the mockup', () => {
    const ticks = timeAxis(lastOf(DAY_MS), 'FIFTEEN_MINUTES');

    expect(axisLabels(ticks)).toEqual([
      '15:00',
      '19:00',
      '23:00',
      '03:00',
      '07:00',
      '11:00',
      '14:30',
    ]);
    expect(ticks.at(-1)?.time).toBe(now.getTime());
  });

  it('labels 1 hour every 15 minutes', () => {
    expect(axisLabels(timeAxis(lastOf(HOUR_MS), 'MINUTE'))).toEqual([
      '13:30',
      '13:45',
      '14:00',
      '14:15',
      '14:30',
    ]);
  });

  it('starts at the next full step when `from` has seconds', () => {
    const range = { from: new Date('2026-11-15T10:30:20Z'), to: new Date('2026-11-15T11:00:00Z') };

    expect(axisLabels(timeAxis(range, 'MINUTE'))).toEqual(['13:45', '14:00']);
  });

  it('labels 6 hours every hour', () => {
    expect(axisLabels(timeAxis(lastOf(6 * HOUR_MS), 'FIVE_MINUTES'))).toEqual([
      '09:00',
      '10:00',
      '11:00',
      '12:00',
      '13:00',
      '14:00',
      '14:30',
    ]);
  });

  it('labels 7 days with the dates of local midnights', () => {
    const ticks = timeAxis(lastOf(7 * DAY_MS), 'HOUR');

    expect(axisLabels(ticks)).toEqual([
      '9 Nov',
      '10 Nov',
      '11 Nov',
      '12 Nov',
      '13 Nov',
      '14 Nov',
      '15 Nov',
    ]);
    expect(new Date(ticks[0]?.time ?? 0).toISOString()).toBe('2026-11-08T21:00:00.000Z');
  });

  it('starts at `from` when it is a local midnight', () => {
    const range = { from: new Date('2026-11-08T21:00:00Z'), to: new Date('2026-11-13T21:00:00Z') };

    expect(axisLabels(timeAxis(range, 'HOUR'))).toEqual([
      '9 Nov',
      '10 Nov',
      '11 Nov',
      '12 Nov',
      '13 Nov',
    ]);
  });

  it('labels 31 days of hours every 7 days', () => {
    expect(axisLabels(timeAxis(lastOf(31 * DAY_MS), 'HOUR'))).toEqual([
      '16 Oct',
      '23 Oct',
      '30 Oct',
      '6 Nov',
      '13 Nov',
    ]);
  });

  it('labels DAY buckets at their UTC midnights with the UTC date', () => {
    const ticks = timeAxis(lastOf(31 * DAY_MS), 'DAY');

    expect(axisLabels(ticks)).toEqual(['15 Oct', '22 Oct', '29 Oct', '5 Nov', '12 Nov']);
    expect(new Date(ticks[0]?.time ?? 0).toISOString()).toBe('2026-10-15T00:00:00.000Z');
  });

  it('labels a few DAY buckets one by one', () => {
    expect(axisLabels(timeAxis(lastOf(3 * DAY_MS), 'DAY'))).toEqual([
      '12 Nov',
      '13 Nov',
      '14 Nov',
      '15 Nov',
    ]);
  });

  it('keeps local midnights across the end of daylight saving time', () => {
    vi.stubEnv('TZ', 'Europe/Berlin');
    const range = { from: new Date('2026-10-22T12:00:00Z'), to: new Date('2026-10-28T12:00:00Z') };

    const ticks = timeAxis(range, 'HOUR');

    expect(axisLabels(ticks)).toEqual(['23 Oct', '24 Oct', '25 Oct', '26 Oct', '27 Oct', '28 Oct']);
    // 25 Oct has 25 hours.
    expect((ticks[3]?.time ?? 0) - (ticks[2]?.time ?? 0)).toBe(25 * HOUR_MS);
  });

  it('takes the longest step when even that gives too many ticks', () => {
    expect(timeAxis(lastOf(31 * DAY_MS), 'HOUR', 1)).toHaveLength(2);
    expect(timeAxis(lastOf(31 * DAY_MS), 'DAY', 1)).toHaveLength(2);
  });
});
