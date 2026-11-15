import { describe, expect, it, vi } from 'vitest';

import { localHourTicks } from './axisTicks';
import { formatDateTime } from './formatting';

function labels(ticks: Date[]): string[] {
  return ticks.map(formatDateTime);
}

describe('localHourTicks', () => {
  it('starts at the first full local hour and steps on the local clock', () => {
    vi.stubEnv('TZ', 'Europe/Moscow');
    // 14:30 the day before until 14:30 in Moscow.
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
    // The step over the change is 5 hours long.
    expect((ticks[1]?.getTime() ?? 0) - (ticks[0]?.getTime() ?? 0)).toBe(5 * 3_600_000);
  });

  it('is empty when the range holds no full hour', () => {
    vi.stubEnv('TZ', 'Europe/Moscow');
    const range = { from: new Date('2026-11-15T10:10:00Z'), to: new Date('2026-11-15T10:50:00Z') };

    expect(localHourTicks(range, 1)).toEqual([]);
  });
});
