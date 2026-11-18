import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { CLOCK_INTERVAL_MS, useNow } from './useNow';

describe('useNow', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-11-18T12:00:00Z'));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('starts at the time of the first render and moves every 10 s', () => {
    const { result } = renderHook(() => useNow());
    expect(result.current).toEqual(new Date('2026-11-18T12:00:00Z'));

    act(() => {
      vi.advanceTimersByTime(CLOCK_INTERVAL_MS - 1);
    });
    expect(result.current).toEqual(new Date('2026-11-18T12:00:00Z'));

    act(() => {
      vi.advanceTimersByTime(1);
    });
    expect(result.current).toEqual(new Date('2026-11-18T12:00:10Z'));
  });

  it('stops moving after unmount', () => {
    const { unmount } = renderHook(() => useNow());

    unmount();

    expect(vi.getTimerCount()).toBe(0);
  });
});
