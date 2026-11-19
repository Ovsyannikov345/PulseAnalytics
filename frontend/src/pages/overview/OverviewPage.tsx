import { useQuery } from '@apollo/client/react';
import { useMemo } from 'react';

import { PageHeader } from '../../components/PageHeader';
import { StaleNotice } from '../../components/StaleNotice';
import { PageError } from '../../components/states/PageError';
import { Skeleton } from '../../components/states/Skeleton';
import { expectedSamples, POLL_PERIOD_MS } from '../../domain/expectedSamples';
import { formatTime } from '../../domain/formatting';
import { isStale } from '../../domain/freshness';
import { METER_COUNT, METERS_PER_TYPE } from '../../domain/meters';
import { presetRange, type TimeRange } from '../../domain/timeRanges';
import { toIsoWithOffset } from '../../domain/utcOffset';
import {
  OverviewEnergySparklinesDocument,
  OverviewEnergyTrendDocument,
  OverviewIngestionCoverageDocument,
  OverviewLatestReadingsDocument,
  OverviewSampleCountDocument,
} from '../../graphql/generated/graphql';
import { mapWidgetQuery, toWidgetQuery, type WidgetQuery } from '../../graphql/widgetQuery';
import { CoverageCard } from './CoverageCard';
import { SPARKLINE_BUCKET, SPARKLINE_RANGE_MS, toEnergySparklines } from './energySparklines';
import { ENERGY_TREND_BUCKET, toEnergyTrend } from './energyTrend';
import { EnergyTrendCard } from './EnergyTrendCard';
import { COVERAGE_BUCKET, toIngestionCoverage } from './ingestionCoverage';
import { summarizeLatest } from './latestValues';
import { LocationsSection } from './LocationsSection';
import styles from './OverviewPage.module.css';
import { SummaryTiles } from './SummaryTiles';
import { REFRESH_INTERVAL_MS, useOverviewRefresh } from './useOverviewRefresh';

// The windows move with every refresh, so cached results would never be read again and would pile up.
const fetchPolicy = 'no-cache';

function rangeVariables(range: TimeRange) {
  return { from: toIsoWithOffset(range.from), to: toIsoWithOffset(range.to) };
}

function failedRetries(queries: readonly WidgetQuery<unknown>[]): (() => void)[] {
  return queries.flatMap((query) => (query.status === 'error' ? [query.retry] : []));
}

export function OverviewPage() {
  const latestResult = useQuery(OverviewLatestReadingsDocument, { fetchPolicy });
  const now = useOverviewRefresh(latestResult.refetch);

  const lastDay = useMemo(() => presetRange('24h', now), [now]);
  const lastTwoHours = useMemo(
    () => ({ from: new Date(now.getTime() - SPARKLINE_RANGE_MS), to: now }),
    [now],
  );

  const sampleCountResult = useQuery(OverviewSampleCountDocument, {
    variables: rangeVariables(lastDay),
    fetchPolicy,
  });
  const sparklinesResult = useQuery(OverviewEnergySparklinesDocument, {
    variables: { ...rangeVariables(lastTwoHours), bucket: SPARKLINE_BUCKET },
    fetchPolicy,
  });
  const energyTrendResult = useQuery(OverviewEnergyTrendDocument, {
    variables: { ...rangeVariables(lastDay), bucket: ENERGY_TREND_BUCKET },
    fetchPolicy,
  });
  const coverageResult = useQuery(OverviewIngestionCoverageDocument, {
    variables: { ...rangeVariables(lastDay), bucket: COVERAGE_BUCKET },
    fetchPolicy,
  });

  const latest = mapWidgetQuery(toWidgetQuery(latestResult), (data) =>
    summarizeLatest(data.latestReadings),
  );
  const sampleCount = mapWidgetQuery(
    toWidgetQuery(sampleCountResult),
    (data) => data.readingCounts[0]?.count ?? 0,
  );
  const sparklines = mapWidgetQuery(toWidgetQuery(sparklinesResult), (data) =>
    toEnergySparklines(data.energyStatistics, lastTwoHours, SPARKLINE_BUCKET),
  );

  const energyTrend = mapWidgetQuery(toWidgetQuery(energyTrendResult), (data) =>
    toEnergyTrend(data.energyStatistics, lastDay, ENERGY_TREND_BUCKET),
  );
  // Each poll stores a reading of every meter, so one type's samples measure the polls.
  const coverage = mapWidgetQuery(toWidgetQuery(coverageResult), (data) =>
    toIngestionCoverage(data.motionStatistics, lastDay, COVERAGE_BUCKET, METERS_PER_TYPE),
  );

  const queries = [latest, sampleCount, sparklines, energyTrend, coverage];
  const retries = failedRetries(queries);
  const latestReceivedAt = latest.status === 'ready' ? latest.data.latestReceivedAt : null;

  return (
    <>
      <PageHeader
        title="Overview"
        description="The latest sample of every meter, with trends for the last 24 hours."
      >
        {latest.status === 'loading' ? <Skeleton width="240px" height="18px" /> : null}
        {latestReceivedAt ? (
          <span className={styles.meta}>
            Latest samples received at {formatTime(latestReceivedAt)}
          </span>
        ) : null}
      </PageHeader>

      {retries.length === queries.length ? (
        <PageError
          description={`The API gateway didn't answer. The page tries again every ${String(REFRESH_INTERVAL_MS / 1000)} s.`}
          onRetry={() => {
            for (const retry of retries) {
              retry();
            }
          }}
        />
      ) : (
        <>
          {latestReceivedAt && isStale(latestReceivedAt, now) ? (
            <StaleNotice latestReceivedAt={latestReceivedAt} now={now} />
          ) : null}
          <SummaryTiles
            latest={latest}
            sampleCount={sampleCount}
            expectedSamples={expectedSamples(lastDay, METER_COUNT)}
          />
          <LocationsSection latest={latest} trend={sparklines} />
          <section aria-label="Trends" className={styles.trends}>
            <EnergyTrendCard trend={energyTrend} range={lastDay} />
            <CoverageCard coverage={coverage} />
          </section>
          <footer className={styles.footer}>
            Every value is a sample: meters are polled every {String(POLL_PERIOD_MS / 1000)} s and
            stamped with the time they were received.
          </footer>
        </>
      )}
    </>
  );
}
