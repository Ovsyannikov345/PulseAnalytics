import { beforeEach, describe, expect, it, vi } from 'vitest';

import { chartAxis, describeRange } from './chartAxis';

// 14:10–15:10 in Moscow.
const range = { from: new Date('2026-11-15T11:10:00Z'), to: new Date('2026-11-15T12:10:00Z') };

describe('chartAxis', () => {
  beforeEach(() => {
    vi.stubEnv('TZ', 'Europe/Moscow');
  });

  it('starts at the first bucket and ends at the end of the range', () => {
    expect(chartAxis(range, 'FIFTEEN_MINUTES').domain).toEqual([
      Date.parse('2026-11-15T11:00:00Z'),
      Date.parse('2026-11-15T12:10:00Z'),
    ]);
  });

  it('labels its ticks and nothing else', () => {
    const { ticks, formatTick } = chartAxis(range, 'FIFTEEN_MINUTES');

    expect(ticks.map(formatTick)).toEqual(['14:15', '14:30', '14:45', '15:00', '15:10']);
    expect(formatTick(Date.parse('2026-11-15T11:20:00Z'))).toBe('');
  });
});

describe('describeRange', () => {
  it('names both ends in local time', () => {
    vi.stubEnv('TZ', 'Europe/Moscow');

    expect(describeRange(range)).toBe('from 15 Nov 14:10 to 15 Nov 15:10');
  });
});
