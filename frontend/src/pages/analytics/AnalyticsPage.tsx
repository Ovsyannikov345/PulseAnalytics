import { useMemo } from 'react';
import { useLocation, useSearchParams } from 'react-router';

import { PageHeader } from '../../components/PageHeader';
import {
  effectiveBucket,
  parseAnalyticsFilter,
  resolveRange,
  toSearchParams,
  type AnalyticsFilter,
} from './analyticsFilter';
import { FilterBar } from './FilterBar';
import { appliedNowState, useAppliedNow } from './useAppliedNow';

export function AnalyticsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const now = useAppliedNow(useLocation().state);

  const filter = useMemo(() => parseAnalyticsFilter(searchParams), [searchParams]);
  const range = useMemo(() => resolveRange(filter.range, now), [filter.range, now]);
  const bucket = effectiveBucket(filter.bucket, range);

  function applyFilter(next: AnalyticsFilter) {
    const params = toSearchParams(next);
    // The time is taken here, not while rendering. Applying the same filters again takes a new time
    // without adding a history entry.
    setSearchParams(params, {
      replace: params.toString() === searchParams.toString(),
      state: appliedNowState(),
    });
  }

  return (
    <>
      <PageHeader
        title="Analytics"
        description="Average, minimum and maximum per location and time bucket. Buckets sit on a fixed UTC grid, so they don't move as the range follows the current time."
      />
      <FilterBar
        filter={filter}
        range={range}
        bucket={bucket}
        invalidInput={false}
        onChange={applyFilter}
      />
    </>
  );
}
