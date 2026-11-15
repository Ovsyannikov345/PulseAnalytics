import { NetworkStatus } from '@apollo/client';
import { useQuery } from '@apollo/client/react';
import { useState } from 'react';

import type { TimeRange } from '../../domain/timeRanges';
import { ReadingsTableDocument, type ReadingFilterInput } from '../../graphql/generated/graphql';
import { useWidgetQuery } from '../../graphql/useWidgetQuery';
import type { WidgetQuery } from '../../graphql/widgetQuery';
import { toReadingsPageData, type ReadingsPageData } from './readingRows';

/** "Load at a time"; the gateway returns at most 100 readings per page. */
export const pageSizes = [25, 50, 100] as const;

export type PageSize = (typeof pageSizes)[number];

export interface ReadingsTable {
  table: WidgetQuery<ReadingsPageData>;
  /** The query's error, for the page's error handling. */
  error: unknown;
  /** Loads the filter again from its first page. */
  refetch: () => Promise<unknown>;
  loadMore: () => void;
  loadingMore: boolean;
  /** Loading the page after the last one failed; `loadMore` tries again. */
  loadMoreFailed: boolean;
}

/**
 * The readings of a filter, page by page. The cache keeps the loaded pages per filter
 * (`relayStylePagination(["filter"])`), so `pageSize` only sizes the pages still to load: changing it
 * reads the pages loaded so far from the cache.
 */
export function useReadingsTable(
  filter: ReadingFilterInput,
  range: TimeRange,
  pageSize: PageSize,
): ReadingsTable {
  const result = useQuery(ReadingsTableDocument, {
    variables: { filter, first: pageSize },
    notifyOnNetworkStatusChange: true,
  });
  // The cursor whose next page failed; a newer cursor or another filter clears the failure.
  const [failedAfter, setFailedAfter] = useState<string | null>(null);

  const loadingMore = result.networkStatus === NetworkStatus.fetchMore;
  // Loading more keeps the loaded rows as they are: not dimmed like a new filter's.
  const table = useWidgetQuery({ ...result, loading: result.loading && !loadingMore }, (data) =>
    toReadingsPageData(data.readings, range),
  );
  const endCursor = result.data?.readings?.pageInfo.endCursor ?? null;

  function loadMore() {
    setFailedAfter(null);
    result.fetchMore({ variables: { after: endCursor, first: pageSize } }).catch(() => {
      setFailedAfter(endCursor);
    });
  }

  return {
    table,
    error: result.error,
    refetch: () => result.refetch(),
    loadMore,
    loadingMore,
    loadMoreFailed: failedAfter !== null && failedAfter === endCursor,
  };
}
