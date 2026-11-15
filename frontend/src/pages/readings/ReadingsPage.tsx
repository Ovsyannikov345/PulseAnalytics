import { useQuery } from '@apollo/client/react';
import { useMemo, useState } from 'react';
import { useSearchParams } from 'react-router';

import { PageHeader } from '../../components/PageHeader';
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

export function ReadingsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  // The default range ends at the time the page opened; renders never take the time themselves.
  const [openedAt] = useState(() => new Date());
  const [resets, setResets] = useState(0);

  const search = searchParams.toString();
  const filter = useMemo(
    () => parseReadingsFilter(searchParams, openedAt),
    [searchParams, openedAt],
  );
  const variables = { filter: readingFilterInput(filter) };

  const countsResult = useQuery(ReadingsMatchCountsDocument, { variables });
  const counts = useWidgetQuery(countsResult, (data) =>
    toMatchCounts(data.readingCounts, filter.types),
  );

  const results = [countsResult];
  const invalidInput = results.some(
    (result) => result.error !== undefined && getQueryErrorKind(result.error) === 'invalidInput',
  );

  function applyFilter(next: ReadingsFilter) {
    const params = toSearchParams(next);
    if (params.toString() === search) {
      // The same filter again loads it again: readings stored meanwhile show up. No history entry.
      for (const result of results) {
        result.refetch().catch(() => undefined);
      }
    }
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
      <ReadingsFilterForm
        // An applied filter (or one from the back button) refills the form; so does Reset, also when
        // it applies the filter that is already applied.
        key={`${search}#${String(resets)}`}
        filter={filter}
        invalidInput={invalidInput}
        onApply={applyFilter}
        onReset={reset}
      >
        <MatchCounts counts={counts} />
      </ReadingsFilterForm>
    </>
  );
}
