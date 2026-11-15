import { useQuery } from '@apollo/client/react';
import { useMemo } from 'react';
import { useLocation, useSearchParams } from 'react-router';

import { PageHeader } from '../../components/PageHeader';
import { PageError } from '../../components/states/PageError';
import { getQueryErrorKind } from '../../graphql/errors';
import { AnalyticsEnergyDocument } from '../../graphql/generated/graphql';
import { useWidgetQuery } from '../../graphql/useWidgetQuery';
import type { WidgetQuery } from '../../graphql/widgetQuery';
import {
  effectiveBucket,
  parseAnalyticsFilter,
  resolveRange,
  statisticsVariables,
  toSearchParams,
  type AnalyticsFilter,
} from './analyticsFilter';
import styles from './AnalyticsPage.module.css';
import { EnergyCard } from './EnergyCard';
import { toEnergySeries } from './energySeries';
import { FilterBar } from './FilterBar';
import { appliedNowState, useAppliedNow } from './useAppliedNow';

function failedRetries(queries: readonly WidgetQuery<unknown>[]): (() => void)[] {
  return queries.flatMap((query) => (query.status === 'error' ? [query.retry] : []));
}

export function AnalyticsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const now = useAppliedNow(useLocation().state);

  const filter = useMemo(() => parseAnalyticsFilter(searchParams), [searchParams]);
  const range = useMemo(() => resolveRange(filter.range, now), [filter.range, now]);
  const bucket = effectiveBucket(filter.bucket, range);
  const { location } = filter;
  const variables = statisticsVariables(range, location);

  const energyResult = useQuery(AnalyticsEnergyDocument, { variables: { ...variables, bucket } });
  const energy = useWidgetQuery(energyResult, (data) =>
    toEnergySeries(data.energyStatistics, range, bucket, location),
  );

  const results = [energyResult];
  const queries = [energy];
  const invalidInput = results.some(
    (result) => result.error !== undefined && getQueryErrorKind(result.error) === 'invalidInput',
  );
  const retries = failedRetries(queries);

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
        invalidInput={invalidInput}
        onChange={applyFilter}
      />
      {!invalidInput && retries.length === queries.length ? (
        <PageError
          onRetry={() => {
            for (const retry of retries) {
              retry();
            }
          }}
        />
      ) : (
        <div className={styles.widgets}>
          <EnergyCard energy={energy} location={location} bucket={bucket} />
        </div>
      )}
    </>
  );
}
