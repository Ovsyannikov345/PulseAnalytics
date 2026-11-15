import { describe, expect, it } from 'vitest';

import { isStale, STALE_AFTER_MS } from './freshness';

describe('isStale', () => {
  const now = new Date('2026-11-15T12:00:00Z');

  it('is two minutes', () => {
    expect(STALE_AFTER_MS).toBe(120_000);
  });

  it.each([
    ['5 s old', 5_000, false],
    ['exactly 2 min old', 120_000, false],
    ['2 min and 1 ms old', 120_001, true],
    ['an hour old', 3_600_000, true],
  ])('is %s → %s', (_, ageMs, expected) => {
    expect(isStale(new Date(now.getTime() - ageMs), now)).toBe(expected);
  });

  it('is not stale when the sample looks newer than now (clocks apart)', () => {
    expect(isStale(new Date(now.getTime() + 10_000), now)).toBe(false);
  });
});
