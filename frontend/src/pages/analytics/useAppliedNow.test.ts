import { renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { appliedNowState, useAppliedNow } from './useAppliedNow';

const opened = new Date('2026-11-15T11:30:00Z');
const later = new Date(opened.getTime() + 60_000);

describe('useAppliedNow', () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(opened);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('is the time the page opened, and renders keep it', () => {
    const { result, rerender } = renderHook(({ state }) => useAppliedNow(state), {
      initialProps: { state: null as unknown },
    });

    vi.setSystemTime(later);
    rerender({ state: null });

    expect(result.current).toEqual(opened);
  });

  it('is the time a filter was applied, as the navigation state carries it', () => {
    const { result, rerender } = renderHook(({ state }) => useAppliedNow(state), {
      initialProps: { state: null as unknown },
    });

    vi.setSystemTime(later);
    const state = appliedNowState();
    vi.setSystemTime(later.getTime() + 60_000);
    rerender({ state });

    expect(result.current).toEqual(later);
  });

  it('keeps the same instance while the state stays the same', () => {
    const state = { appliedAt: later.getTime() };
    const { result, rerender } = renderHook(() => useAppliedNow(state));
    const first = result.current;

    rerender();

    expect(result.current).toBe(first);
  });

  it.each([
    ['an entry from before the page opened', { appliedAt: opened.getTime() - 60_000 }],
    ['state of another shape', { appliedAt: 'yesterday' }],
    ['no state', undefined],
  ])('uses the opening time for %s', (_, state) => {
    const { result } = renderHook(() => useAppliedNow(state));

    expect(result.current).toEqual(opened);
  });
});
