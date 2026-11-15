/**
 * What a widget shows for its query:
 * - `loading`: nothing to show yet (skeletons);
 * - `error`: the query failed; `retry` runs it again;
 * - `ready`: data to show. While newer data loads (e.g. a moved time range), the previous data stays.
 */
export type WidgetQuery<TData> =
  { status: 'loading' } | { status: 'error'; retry: () => void } | { status: 'ready'; data: TData };

/** The parts of Apollo's `useQuery` result that decide the widget's state. */
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
        // A failed retry shows up as the query's error; the promise's rejection adds nothing.
        result.refetch().catch(() => undefined);
      },
    };
  }

  const data = result.data ?? result.previousData;
  return data === undefined ? { status: 'loading' } : { status: 'ready', data };
}

/** Applies `map` to the data of a ready query. */
export function mapWidgetQuery<TData, TResult>(
  query: WidgetQuery<TData>,
  map: (data: TData) => TResult,
): WidgetQuery<TResult> {
  return query.status === 'ready' ? { status: 'ready', data: map(query.data) } : query;
}
