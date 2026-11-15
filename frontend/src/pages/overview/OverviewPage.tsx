import { useQuery } from '@apollo/client/react';
import { useMemo } from 'react';

import { PageHeader } from '../../components/PageHeader';
import { PageError } from '../../components/states/PageError';
import { Skeleton } from '../../components/states/Skeleton';
import { expectedSamples, POLL_PERIOD_MS } from '../../domain/expectedSamples';
import { formatTime } from '../../domain/formatting';
import { isStale } from '../../domain/freshness';
import { METER_COUNT } from '../../domain/meters';
import { presetRange, type TimeRange } from '../../domain/timeRanges';
import { toIsoWithOffset } from '../../domain/utcOffset';
import {
  OverviewEnergySparklinesDocument,
  OverviewLatestReadingsDocument,
  OverviewSampleCountDocument,
} from '../../graphql/generated/graphql';
import { mapWidgetQuery, toWidgetQuery, type WidgetQuery } from '../../graphql/widgetQuery';
import { SPARKLINE_BUCKET, SPARKLINE_RANGE_MS, toEnergySparklines } from './energySparklines';
import { summarizeLatest } from './latestValues';
import { LocationsSection } from './LocationsSection';
import styles from './OverviewPage.module.css';
import { StaleNotice } from './StaleNotice';
import { SummaryTiles } from './SummaryTiles';
import { REFRESH_INTERVAL_MS, useOverviewRefresh } from './useOverviewRefresh';

/**
 * The time ranges move with every refresh, so a cached result would never be read again and a page
 * left open would pile them up; the readings' entities would pile up in the normalized cache too.
 */
const fetchPolicy = 'no-cache';

function rangeVariables(range: TimeRange) {
  return { from: toIsoWithOffset(range.from), to: toIsoWithOffset(range.to) };
}

/** The retries of the failed queries. */
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

  const latest = mapWidgetQuery(toWidgetQuery(latestResult), (data) =>
    summarizeLatest(data.latestReadings),
  );
  // An empty `groupBy` returns one row with the total.
  const sampleCount = mapWidgetQuery(
    toWidgetQuery(sampleCountResult),
    (data) => data.readingCounts[0]?.count ?? 0,
  );
  const sparklines = mapWidgetQuery(toWidgetQuery(sparklinesResult), (data) =>
    toEnergySparklines(data.energyStatistics, lastTwoHours, SPARKLINE_BUCKET),
  );

  const queries = [latest, sampleCount, sparklines];
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

      {/* Every query failed: the gateway is unreachable. The refreshes go on, so the page recovers. */}
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
          <footer className={styles.footer}>
            Every value is a sample: meters are polled every {String(POLL_PERIOD_MS / 1000)} s and
            stamped with the time they were received.
          </footer>
        </>
      )}
    </>
  );
}
