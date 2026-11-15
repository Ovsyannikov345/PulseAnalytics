import { describe, expect, it } from 'vitest';

import { toMatchCounts } from './readingCounts';

function row(type: 'AIR_QUALITY' | 'ENERGY' | 'MOTION', count: number) {
  return { __typename: 'ReadingCount' as const, type, count };
}

describe('toMatchCounts', () => {
  it('lists the selected types in their order with the total', () => {
    expect(
      toMatchCounts([row('MOTION', 30), row('AIR_QUALITY', 10)], ['AIR_QUALITY', 'MOTION']),
    ).toEqual({
      total: 40,
      byType: [
        { type: 'AIR_QUALITY', count: 10 },
        { type: 'MOTION', count: 30 },
      ],
    });
  });

  it('counts 0 for a type the gateway left out', () => {
    expect(toMatchCounts([row('ENERGY', 5)], ['ENERGY', 'MOTION'])).toEqual({
      total: 5,
      byType: [
        { type: 'ENERGY', count: 5 },
        { type: 'MOTION', count: 0 },
      ],
    });
  });

  it('is 0 without readings', () => {
    expect(toMatchCounts([], ['ENERGY'])).toEqual({
      total: 0,
      byType: [{ type: 'ENERGY', count: 0 }],
    });
  });
});
