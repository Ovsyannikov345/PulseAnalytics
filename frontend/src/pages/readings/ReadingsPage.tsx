import { useMemo, useState } from 'react';
import { useSearchParams } from 'react-router';

import { PageHeader } from '../../components/PageHeader';
import {
  defaultReadingsFilter,
  parseReadingsFilter,
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

  function applyFilter(next: ReadingsFilter) {
    const params = toSearchParams(next);
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
      <ReadingsFilterForm
        // An applied filter (or one from the back button) refills the form; so does Reset, also when
        // it applies the filter that is already applied.
        key={`${search}#${String(resets)}`}
        filter={filter}
        onApply={applyFilter}
        onReset={reset}
      />
    </>
  );
}
