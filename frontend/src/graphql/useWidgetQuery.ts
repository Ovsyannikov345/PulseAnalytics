import { useState } from 'react';

import { toWidgetQuery, type QueryResultLike, type WidgetQuery } from './widgetQuery';

/**
 * `toWidgetQuery` plus a view model. Each response is mapped once, when it arrives, so `map` sees the
 * filters it was loaded for. While newer data loads, the previous view model stays on screen as it was:
 * mapped again with the new filters, previous data would land on a grid it wasn't loaded for.
 */
export function useWidgetQuery<TData, TResult>(
  result: QueryResultLike<TData>,
  map: (data: TData) => TResult,
): WidgetQuery<TResult> {
  const [mapped, setMapped] = useState<{ data: TData; value: TResult } | null>(null);

  let current = mapped;
  if (result.data !== undefined && mapped?.data !== result.data) {
    // Adjusting state while rendering: React re-renders at once with the new view model.
    current = { data: result.data, value: map(result.data) };
    setMapped(current);
  }

  const query = toWidgetQuery(result);
  if (query.status !== 'ready') {
    return query;
  }

  return { ...query, data: current ? current.value : map(query.data) };
}
