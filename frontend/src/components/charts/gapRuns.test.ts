import { describe, expect, it } from 'vitest';

import { gapRuns } from './gapRuns';

const points = (values: (number | null)[]) =>
  values.map((value, index) => ({ time: index * 10, value }));

describe('gapRuns', () => {
  it('joins consecutive empty buckets into one run, from the first start to the last end', () => {
    expect(gapRuns(points([1, null, null, 2, null, 3]), 10)).toEqual([
      { from: 10, to: 30 },
      { from: 40, to: 50 },
    ]);
  });

  it('includes empty buckets at both ends', () => {
    expect(gapRuns(points([null, 1, null]), 10)).toEqual([
      { from: 0, to: 10 },
      { from: 20, to: 30 },
    ]);
  });

  it('has no runs without empty buckets', () => {
    expect(gapRuns(points([1, 2, 3]), 10)).toEqual([]);
    expect(gapRuns([], 10)).toEqual([]);
  });

  it('is one run when every bucket is empty', () => {
    expect(gapRuns(points([null, null]), 10)).toEqual([{ from: 0, to: 20 }]);
  });
});
