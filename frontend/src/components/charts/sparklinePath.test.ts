import { describe, expect, it } from 'vitest';

import { sparklinePath } from './sparklinePath';

describe('sparklinePath', () => {
  it('spreads the values across the width, from 0 at the bottom to max at the top', () => {
    // 2 px padding: x from 2 to 98, y from 18 (0) to 2 (max).
    expect(sparklinePath([0, 50, 100], 100, 100, 20)).toBe('M2.0,18.0L50.0,10.0L98.0,2.0');
  });

  it('breaks the line at gaps', () => {
    expect(sparklinePath([10, 20, null, 30, 40], 40, 104, 20)).toBe(
      'M2.0,14.0L27.0,10.0M77.0,6.0L102.0,2.0',
    );
  });

  it('draws an isolated value as a dot', () => {
    expect(sparklinePath([null, 20, null], 40, 100, 20)).toBe('M50.0,10.0h0');
  });

  it('draws a single value in the middle', () => {
    expect(sparklinePath([40], 40, 100, 20)).toBe('M50.0,2.0h0');
  });

  it('clamps values outside 0 to max', () => {
    expect(sparklinePath([-5, 150], 100, 100, 20)).toBe('M2.0,18.0L98.0,2.0');
  });

  it('puts everything on the bottom line when max is 0', () => {
    expect(sparklinePath([0, 0], 0, 100, 20)).toBe('M2.0,18.0L98.0,18.0');
  });

  it('is empty without values', () => {
    expect(sparklinePath([], 100, 100, 20)).toBe('');
    expect(sparklinePath([null, null], 100, 100, 20)).toBe('');
  });
});
