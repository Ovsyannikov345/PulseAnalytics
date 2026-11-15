import { useQuery } from '@apollo/client/react';
import { useMemo } from 'react';
import { useLocation, useSearchParams } from 'react-router';

import { PageHeader } from '../../components/PageHeader';
import { PageError } from '../../components/states/PageError';
import { getQueryErrorKind } from '../../graphql/errors';
import {
  AnalyticsAirQualityDocument,
  AnalyticsAirQualitySummaryDocument,
  AnalyticsEnergyDocument,
  AnalyticsEnergySummaryDocument,
  AnalyticsMotionActivityDocument,
  AnalyticsMotionSummaryDocument,
} from '../../graphql/generated/graphql';
import { useWidgetQuery } from '../../graphql/useWidgetQuery';
import type { WidgetQuery } from '../../graphql/widgetQuery';
import { AirQualitySection } from './AirQualitySection';
import { toAirQualitySeries } from './airQualitySeries';
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
import { toAirQualitySummary, toEnergySummary, toMotionSummary } from './locationSummary';
import { LocationSummaryCard } from './LocationSummaryCard';
import { toMotionActivity } from './motionActivity';
import { MotionActivityCard } from './MotionActivityCard';
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

  const airQualityResult = useQuery(AnalyticsAirQualityDocument, {
    variables: { ...variables, bucket },
  });
  const airQuality = useWidgetQuery(airQualityResult, (data) =>
    toAirQualitySeries(data.airQualityStatistics, range, bucket, location),
  );

  const motionResult = useQuery(AnalyticsMotionActivityDocument, {
    variables: { ...variables, bucket },
  });
  const motion = useWidgetQuery(motionResult, (data) =>
    toMotionActivity(data.motionStatistics, range, bucket, location),
  );

  const energySummaryResult = useQuery(AnalyticsEnergySummaryDocument, { variables });
  const energySummary = useWidgetQuery(energySummaryResult, (data) =>
    toEnergySummary(data.energyStatistics),
  );
  const airQualitySummaryResult = useQuery(AnalyticsAirQualitySummaryDocument, { variables });
  const airQualitySummary = useWidgetQuery(airQualitySummaryResult, (data) =>
    toAirQualitySummary(data.airQualityStatistics),
  );
  const motionSummaryResult = useQuery(AnalyticsMotionSummaryDocument, { variables });
  const motionSummary = useWidgetQuery(motionSummaryResult, (data) =>
    toMotionSummary(data.motionStatistics),
  );

  const results = [
    energyResult,
    airQualityResult,
    motionResult,
    energySummaryResult,
    airQualitySummaryResult,
    motionSummaryResult,
  ];
  const queries = [energy, airQuality, motion, energySummary, airQualitySummary, motionSummary];
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
          <AirQualitySection airQuality={airQuality} location={location} />
          <MotionActivityCard activity={motion} bucket={bucket} />
          <LocationSummaryCard
            energy={energySummary}
            airQuality={airQualitySummary}
            motion={motionSummary}
            location={location}
          />
        </div>
      )}
    </>
  );
}
