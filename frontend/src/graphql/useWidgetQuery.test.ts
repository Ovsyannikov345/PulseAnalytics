import { renderHook } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { useWidgetQuery } from './useWidgetQuery';
import type { QueryResultLike } from './widgetQuery';

interface Props {
  result: QueryResultLike<{ value: number }>;
  factor: number;
}

function renderWidgetQuery(initialProps: Props) {
  const map = vi.fn((data: { value: number }, factor: number) => data.value * factor);
  const hook = renderHook(
    ({ result, factor }: Props) => useWidgetQuery(result, (data) => map(data, factor)),
    { initialProps },
  );
  return { ...hook, map };
}

const refetch = () => Promise.resolve();

describe('useWidgetQuery', () => {
  it('is loading before the first data', () => {
    const { result } = renderWidgetQuery({ result: { loading: true, refetch }, factor: 1 });

    expect(result.current).toEqual({ status: 'loading' });
  });

  it('maps the data once, when it arrives', () => {
    const data = { value: 2 };
    const { result, rerender, map } = renderWidgetQuery({
      result: { data, loading: false, refetch },
      factor: 10,
    });

    expect(result.current).toEqual({ status: 'ready', data: 20, refreshing: false });

    rerender({ result: { data, loading: false, refetch }, factor: 10 });
    expect(map).toHaveBeenCalledOnce();
  });

  it('keeps the previous view model, mapped with its own filters, while newer data loads', () => {
    const previousData = { value: 2 };
    const { result, rerender } = renderWidgetQuery({
      result: { data: previousData, loading: false, refetch },
      factor: 10,
    });

    rerender({ result: { previousData, loading: true, refetch }, factor: 100 });

    expect(result.current).toEqual({ status: 'ready', data: 20, refreshing: true });
  });

  it('maps newer data with the filters it was loaded for', () => {
    const { result, rerender } = renderWidgetQuery({
      result: { data: { value: 2 }, loading: false, refetch },
      factor: 10,
    });

    rerender({ result: { data: { value: 3 }, loading: false, refetch }, factor: 100 });

    expect(result.current).toEqual({ status: 'ready', data: 300, refreshing: false });
  });

  it('maps previous data it has not seen, e.g. after a remount', () => {
    const { result } = renderWidgetQuery({
      result: { previousData: { value: 4 }, loading: true, refetch },
      factor: 10,
    });

    expect(result.current).toEqual({ status: 'ready', data: 40, refreshing: true });
  });

  it('passes an error through', () => {
    const { result } = renderWidgetQuery({
      result: { error: new Error('down'), loading: false, refetch },
      factor: 10,
    });

    expect(result.current.status).toBe('error');
  });
});
