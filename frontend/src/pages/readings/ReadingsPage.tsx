import { useQuery } from '@apollo/client/react';
import { useMemo, useState } from 'react';
import { useSearchParams } from 'react-router';

import { PageHeader } from '../../components/PageHeader';
import { PageError } from '../../components/states/PageError';
import { getQueryErrorKind } from '../../graphql/errors';
import { ReadingsMatchCountsDocument } from '../../graphql/generated/graphql';
import { useWidgetQuery } from '../../graphql/useWidgetQuery';
import { MatchCounts } from './MatchCounts';
import { toMatchCounts } from './readingCounts';
import {
  defaultReadingsFilter,
  parseReadingsFilter,
  readingFilterInput,
  toSearchParams,
  type ReadingsFilter,
} from './readingsFilter';
import { ReadingsFilterForm } from './ReadingsFilterForm';
import styles from './ReadingsPage.module.css';
import { ResultsCard } from './ResultsCard';
import { pageSizes, useReadingsTable, type PageSize } from './useReadingsTable';

export function ReadingsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  // The default range ends at the time the page opened; renders never take the time themselves.
  const [openedAt] = useState(() => new Date());
  const [resets, setResets] = useState(0);
  const [pageSize, setPageSize] = useState<PageSize>(pageSizes[0]);

  const search = searchParams.toString();
  const filter = useMemo(
    () => parseReadingsFilter(searchParams, openedAt),
    [searchParams, openedAt],
  );
  const filterInput = readingFilterInput(filter);

  const countsResult = useQuery(ReadingsMatchCountsDocument, {
    variables: { filter: filterInput },
  });
  const counts = useWidgetQuery(countsResult, (data) =>
    toMatchCounts(data.readingCounts, filter.types),
  );
  const readings = useReadingsTable(filterInput, filter.range, pageSize);

  const queries = [
    { error: countsResult.error, refetch: countsResult.refetch, status: counts.status },
    { error: readings.error, refetch: readings.refetch, status: readings.table.status },
  ];
  const invalidInput = queries.some(
    ({ error }) => error !== undefined && getQueryErrorKind(error) === 'invalidInput',
  );
  const pageError = !invalidInput && queries.every(({ status }) => status === 'error');

  function loadAgain() {
    for (const { refetch } of queries) {
      // A failure shows up as the query's error.
      refetch().catch(() => undefined);
    }
  }

  function applyFilter(next: ReadingsFilter) {
    const params = toSearchParams(next);
    if (params.toString() === search) {
      // The same filter again loads it again from the first page: readings stored meanwhile show up.
      loadAgain();
    }
    // The same filter again doesn't add a history entry.
    setSearchParams(params, { replace: params.toString() === search });
  }

  function reset() {
    setResets((count) => count + 1);
    applyFilter(defaultReadingsFilter(new Date()));
  }

  return (
    <>
      <PageHeader
        title="Readings"
        description="Every stored sample, newest first. Filter by meter and time, page through the results, or download them as CSV."
      />
      <div className={styles.content}>
        <ReadingsFilterForm
          // An applied filter (or one from the back button) refills the form; so does Reset, also
          // when it applies the filter that is already applied.
          key={`${search}#${String(resets)}`}
          filter={filter}
          invalidInput={invalidInput}
          onApply={applyFilter}
          onReset={reset}
        >
          {pageError ? null : <MatchCounts counts={counts} />}
        </ReadingsFilterForm>
        {pageError ? (
          <PageError onRetry={loadAgain} />
        ) : (
          <ResultsCard
            readings={readings}
            total={counts.status === 'ready' ? counts.data.total : null}
            pageSize={pageSize}
            onPageSizeChange={setPageSize}
            onReset={reset}
          />
        )}
      </div>
    </>
  );
}
