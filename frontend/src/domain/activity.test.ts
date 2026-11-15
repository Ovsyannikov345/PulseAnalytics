import { describe, expect, it } from 'vitest';

import { ACTIVITY_CLASS_COUNT, activityClass, activityClassLabel } from './activity';

describe('activityClass', () => {
  it.each([
    [0, 0],
    [0.199, 0],
    [0.2, 1],
    [0.4, 2],
    [0.6, 3],
    [0.799, 3],
    [0.8, 4],
    [1, 4],
  ])('puts %d in class %d', (ratio, expected) => {
    expect(activityClass(ratio)).toBe(expected);
  });

  it('keeps values outside 0–1 in the outer classes', () => {
    expect(activityClass(-0.1)).toBe(0);
    expect(activityClass(1.5)).toBe(4);
  });
});

describe('activityClassLabel', () => {
  it('names the five classes', () => {
    expect(
      Array.from({ length: ACTIVITY_CLASS_COUNT }, (_, index) => activityClassLabel(index)),
    ).toEqual(['0–20 %', '20–40 %', '40–60 %', '60–80 %', '80–100 %']);
  });
});
