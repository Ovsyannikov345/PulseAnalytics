import { describe, expect, it, vi } from 'vitest';

import { mapWidgetQuery, toWidgetQuery, type WidgetQuery } from './widgetQuery';

function result(overrides: Partial<Parameters<typeof toWidgetQuery<number>>[0]> = {}) {
  return { loading: false, refetch: vi.fn(() => Promise.resolve()), ...overrides };
}

describe('toWidgetQuery', () => {
  it('is loading before the first data', () => {
    expect(toWidgetQuery(result({ loading: true }))).toEqual({ status: 'loading' });
  });

  it('is ready with the data', () => {
    expect(toWidgetQuery(result({ data: 3 }))).toEqual({
      status: 'ready',
      data: 3,
      refreshing: false,
    });
  });

  it('keeps the previous data while newer data loads', () => {
    expect(toWidgetQuery(result({ loading: true, previousData: 2 }))).toEqual({
      status: 'ready',
      data: 2,
      refreshing: true,
    });
  });

  it('prefers the current data over the previous', () => {
    expect(toWidgetQuery(result({ data: 3, previousData: 2 }))).toEqual({
      status: 'ready',
      data: 3,
      refreshing: false,
    });
  });

  it('is ready with a falsy value such as 0', () => {
    expect(toWidgetQuery(result({ data: 0 }))).toEqual({
      status: 'ready',
      data: 0,
      refreshing: false,
    });
  });

  it('shows an error even when previous data exists', () => {
    const query = toWidgetQuery(result({ error: new Error('down'), previousData: 2 }));

    expect(query.status).toBe('error');
  });

  it('is loading again while an error is retried', () => {
    expect(toWidgetQuery(result({ error: new Error('down'), loading: true }))).toEqual({
      status: 'loading',
    });
  });

  it('retries by refetching and ignores a failed refetch', async () => {
    const refetch = vi.fn(() => Promise.reject(new Error('still down')));
    const query = toWidgetQuery(result({ error: new Error('down'), refetch }));

    if (query.status !== 'error') {
      throw new Error('Expected an error state.');
    }
    query.retry();

    expect(refetch).toHaveBeenCalledOnce();
    await Promise.resolve();
  });
});

describe('mapWidgetQuery', () => {
  it('maps ready data and keeps whether it refreshes', () => {
    expect(
      mapWidgetQuery({ status: 'ready', data: 2, refreshing: true }, (value) => value * 10),
    ).toEqual({ status: 'ready', data: 20, refreshing: true });
  });

  it.each<WidgetQuery<number>>([{ status: 'loading' }, { status: 'error', retry: vi.fn() }])(
    'passes $status through',
    (query) => {
      expect(mapWidgetQuery(query, (value) => value * 10)).toBe(query);
    },
  );
});
