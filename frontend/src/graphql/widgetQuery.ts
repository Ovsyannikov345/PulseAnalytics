export type WidgetQuery<TData> =
  { status: 'loading' } | { status: 'error'; retry: () => void } | { status: 'ready'; data: TData };

interface QueryResultLike<TData> {
  data?: TData | undefined;
  previousData?: TData | undefined;
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
  return data === undefined ? { status: 'loading' } : { status: 'ready', data };
}

export function mapWidgetQuery<TData, TResult>(
  query: WidgetQuery<TData>,
  map: (data: TData) => TResult,
): WidgetQuery<TResult> {
  return query.status === 'ready' ? { status: 'ready', data: map(query.data) } : query;
}
