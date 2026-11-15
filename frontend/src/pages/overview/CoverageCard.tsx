import { Card } from '../../components/Card';
import { CoverageBars } from '../../components/charts/CoverageBars';
import { Skeleton } from '../../components/states/Skeleton';
import { WidgetError } from '../../components/states/WidgetError';
import { POLL_PERIOD_MS } from '../../domain/expectedSamples';
import {
  formatDateTime,
  formatHourMinute,
  formatNumber,
  formatPercent,
} from '../../domain/formatting';
import { bucketLengthMs } from '../../domain/timeBuckets';
import type { WidgetQuery } from '../../graphql/widgetQuery';
import styles from './CoverageCard.module.css';
import {
  COVERAGE_BUCKET,
  type CoverageHour,
  lowCoverageHour,
  overallShare,
} from './ingestionCoverage';

/** Full hours starting at 00:00, 06:00, 12:00 and 18:00 local time get a label under their bar. */
const AXIS_LABEL_HOURS = 6;

/** "14:00–15:00" */
function hourSpan({ span }: CoverageHour): string {
  return `${formatHourMinute(span.from)}–${formatHourMinute(span.to)}`;
}

function Bars({ hours }: Readonly<{ hours: CoverageHour[] }>) {
  const lowest = lowCoverageHour(hours);
  const label = `Stored samples per hour as a share of the expected polls over the last 24 hours: ${formatPercent(overallShare(hours))} overall${lowest ? `, lowest ${formatPercent(lowest.share)} at ${hourSpan(lowest)}` : ''}.`;

  return (
    <>
      <CoverageBars
        label={label}
        bars={hours.map((hour) => ({
          key: String(hour.span.from.getTime()),
          share: hour.share,
          title: `${formatDateTime(hour.span.from)}–${formatHourMinute(hour.span.to)} · ${formatNumber(hour.samples)} of ${formatNumber(hour.expected)} samples (${formatPercent(hour.share)})`,
          axisLabel:
            hour.full && hour.span.from.getHours() % AXIS_LABEL_HOURS === 0
              ? formatHourMinute(hour.span.from)
              : undefined,
        }))}
      />
      {lowest ? (
        <p className={styles.note}>
          <svg
            aria-hidden="true"
            width="16"
            height="16"
            viewBox="0 0 16 16"
            className={styles.icon}
          >
            <circle cx="8" cy="8" r="7" fill="none" strokeWidth="1.3" />
            <path d="M8 7v4.5M8 4.6v.4" strokeWidth="1.5" strokeLinecap="round" />
          </svg>
          <span>
            {hourSpan(lowest)} is lowest at {formatPercent(lowest.share)}: most polls failed or
            their samples weren't stored.
          </span>
        </p>
      ) : null}
    </>
  );
}

interface CoverageCardProps {
  coverage: WidgetQuery<CoverageHour[]>;
}

export function CoverageCard({ coverage }: Readonly<CoverageCardProps>) {
  const pollsPerHour = bucketLengthMs(COVERAGE_BUCKET) / POLL_PERIOD_MS;

  return (
    <Card
      title="Ingestion coverage"
      description={`Stored samples per hour as a share of the expected ${formatNumber(pollsPerHour)} polls`}
      busy={coverage.status === 'loading'}
      className={styles.card}
    >
      {coverage.status === 'loading' ? <Skeleton height="224px" /> : null}
      {coverage.status === 'error' ? (
        <WidgetError
          message="This chart couldn't be loaded."
          onRetry={coverage.retry}
          className={styles.error}
        />
      ) : null}
      {coverage.status === 'ready' ? <Bars hours={coverage.data} /> : null}
    </Card>
  );
}
