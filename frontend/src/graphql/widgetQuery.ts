/** `refreshing`: newer data is loading while this data stays on screen. */
export type WidgetQuery<TData> =
  | { status: 'loading' }
  | { status: 'error'; retry: () => void }
  | { status: 'ready'; data: TData; refreshing: boolean };

export interface QueryResultLike<TData> {
  data?: TData;
  previousData?: TData;
  error?: unknown;
  loading: boolean;
  refetch: () => Promise<unknown>;
}

export function toWidgetQuery<TData>(result: QueryResultLike<TData>): WidgetQuery<TData> {
  if (result.error && !result.loading) {
    return {
      status: 'error',
      retry: () => {
        // A failed retry shows up as the query's error.
        result.refetch().catch(() => undefined);
      },
    };
  }

  // Apollo clears `data` while new variables load; the previous data stays on screen meanwhile.
  const data = result.data ?? result.previousData;
  return data === undefined
    ? { status: 'loading' }
    : { status: 'ready', data, refreshing: result.loading };
}

export function mapWidgetQuery<TData, TResult>(
  query: WidgetQuery<TData>,
  map: (data: TData) => TResult,
): WidgetQuery<TResult> {
  return query.status === 'ready' ? { ...query, data: map(query.data) } : query;
}
