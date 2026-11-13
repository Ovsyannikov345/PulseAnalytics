import { describe, expect, it } from 'vitest';

import { cx } from './cx';

describe('cx', () => {
  it('joins the given class names with spaces', () => {
    expect(cx('a', 'b', 'c')).toBe('a b c');
  });

  it('skips false, null, undefined and empty names', () => {
    expect(cx('a', false, null, undefined, '', 'b')).toBe('a b');
  });

  it('returns an empty string when nothing is left', () => {
    expect(cx(false, undefined)).toBe('');
  });
});
