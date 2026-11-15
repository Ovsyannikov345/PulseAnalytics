import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { REFRESH_INTERVAL_MS, useOverviewRefresh } from './useOverviewRefresh';

const start = new Date('2026-11-15T12:00:00Z');

let visibility: DocumentVisibilityState = 'visible';

function setVisibility(state: DocumentVisibilityState) {
  visibility = state;
  document.dispatchEvent(new Event('visibilitychange'));
}

function after(milliseconds: number): Date {
  return new Date(start.getTime() + milliseconds);
}

/** Advances fake time and lets the refresh's promise chain settle inside `act`. */
async function advance(milliseconds: number) {
  await act(() => vi.advanceTimersByTimeAsync(milliseconds));
}

function deferred() {
  let resolve: () => void = () => undefined;
  const promise = new Promise<void>((resolvePromise) => {
    resolve = resolvePromise;
  });
  return { promise, resolve };
}

async function settle(pending: ReturnType<typeof deferred>) {
  await act(async () => {
    pending.resolve();
    await pending.promise;
  });
}

describe('useOverviewRefresh', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(start);
    visibility = 'visible';
    vi.spyOn(document, 'visibilityState', 'get').mockImplementation(() => visibility);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('starts at the time the page opens', () => {
    const refetch = vi.fn(() => Promise.resolve());
    const { result } = renderHook(() => useOverviewRefresh(refetch));

    expect(result.current).toEqual(start);
    expect(refetch).not.toHaveBeenCalled();
  });

  it('refetches the latest readings and moves on every 30 s while visible', async () => {
    const refetch = vi.fn(() => Promise.resolve());
    const { result } = renderHook(() => useOverviewRefresh(refetch));

    await advance(REFRESH_INTERVAL_MS - 1);
    expect(refetch).not.toHaveBeenCalled();

    await advance(1);
    expect(refetch).toHaveBeenCalledOnce();
    expect(result.current).toEqual(after(REFRESH_INTERVAL_MS));

    await advance(REFRESH_INTERVAL_MS);
    expect(refetch).toHaveBeenCalledTimes(2);
    expect(result.current).toEqual(after(2 * REFRESH_INTERVAL_MS));
  });

  it('moves only once the latest readings are back, to the time the refresh began', async () => {
    const pending = deferred();
    const refetch = vi.fn(() => pending.promise);
    const { result } = renderHook(() => useOverviewRefresh(refetch));

    await advance(REFRESH_INTERVAL_MS);
    expect(refetch).toHaveBeenCalledOnce();
    expect(result.current).toEqual(start);

    await advance(2_000);
    await settle(pending);

    expect(result.current).toEqual(after(REFRESH_INTERVAL_MS));
  });

  it('still moves when the refetch fails', async () => {
    const refetch = vi.fn(() => Promise.reject(new Error('gateway down')));
    const { result } = renderHook(() => useOverviewRefresh(refetch));

    await advance(REFRESH_INTERVAL_MS);

    expect(result.current).toEqual(after(REFRESH_INTERVAL_MS));
  });

  it('makes no requests while hidden and refreshes at once when shown again', async () => {
    const refetch = vi.fn(() => Promise.resolve());
    const { result } = renderHook(() => useOverviewRefresh(refetch));

    setVisibility('hidden');
    await advance(5 * REFRESH_INTERVAL_MS);
    expect(refetch).not.toHaveBeenCalled();
    expect(result.current).toEqual(start);

    setVisibility('visible');
    await advance(0);

    expect(refetch).toHaveBeenCalledOnce();
    expect(result.current).toEqual(after(5 * REFRESH_INTERVAL_MS));
  });

  it('waits for the rest of the interval when shown again before a refresh was due', async () => {
    const refetch = vi.fn(() => Promise.resolve());
    renderHook(() => useOverviewRefresh(refetch));

    await advance(10_000);
    setVisibility('hidden');
    await advance(5_000);
    setVisibility('visible');

    await advance(REFRESH_INTERVAL_MS - 15_000 - 1);
    expect(refetch).not.toHaveBeenCalled();
    await advance(1);
    expect(refetch).toHaveBeenCalledOnce();
  });

  it('does not start a second refresh while one is running', async () => {
    const pending = deferred();
    const refetch = vi.fn(() => pending.promise);
    renderHook(() => useOverviewRefresh(refetch));

    await advance(REFRESH_INTERVAL_MS);
    setVisibility('hidden');
    setVisibility('visible');
    await advance(REFRESH_INTERVAL_MS);

    expect(refetch).toHaveBeenCalledOnce();
    await settle(pending);
  });

  it('stops when the page closes', async () => {
    const refetch = vi.fn(() => Promise.resolve());
    const { unmount } = renderHook(() => useOverviewRefresh(refetch));

    unmount();
    setVisibility('hidden');
    setVisibility('visible');
    await advance(3 * REFRESH_INTERVAL_MS);

    expect(refetch).not.toHaveBeenCalled();
  });

  it('does not move or go on after the page closed during a refresh', async () => {
    const pending = deferred();
    const refetch = vi.fn(() => pending.promise);
    const { result, unmount } = renderHook(() => useOverviewRefresh(refetch));

    await advance(REFRESH_INTERVAL_MS);
    unmount();
    await settle(pending);
    await advance(REFRESH_INTERVAL_MS);

    expect(result.current).toEqual(start);
    expect(refetch).toHaveBeenCalledOnce();
  });
});
