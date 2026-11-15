import { Card } from '../../components/Card';
import { gapRuns } from '../../components/charts/gapRuns';
import { TimeSeriesChart } from '../../components/charts/TimeSeriesChart';
import { EmptyState } from '../../components/states/EmptyState';
import { Skeleton } from '../../components/states/Skeleton';
import { WidgetError } from '../../components/states/WidgetError';
import { localHourTicks } from '../../domain/axisTicks';
import { formatBucketRange, formatHourMinute, formatNumber } from '../../domain/formatting';
import { bucketLengthMs, bucketStartOf, timeBucketLabel } from '../../domain/timeBuckets';
import type { TimeRange } from '../../domain/timeRanges';
import type { WidgetQuery } from '../../graphql/widgetQuery';
import styles from './EnergyTrendCard.module.css';
import { describeEnergyTrend, ENERGY_TREND_BUCKET, type EnergyTrendPoint } from './energyTrend';

const CHART_HEIGHT = 270;
const TICK_STEP_HOURS = 4;

function formatTick(time: number): string {
  return formatHourMinute(new Date(time));
}

function formatValue(value: number): string {
  return formatNumber(value);
}

export function EnergyTooltip({ point }: Readonly<{ point: EnergyTrendPoint }>) {
  return (
    <>
      <p className={styles.tooltipTitle}>
        {formatBucketRange(new Date(point.time), ENERGY_TREND_BUCKET)}
      </p>
      {point.bucket ? (
        <dl className={styles.tooltipRows}>
          <div className={styles.tooltipRow}>
            <dt>Average</dt>
            <dd>{formatNumber(point.bucket.average, 2)}</dd>
          </div>
          <div className={styles.tooltipRow}>
            <dt>Min – max</dt>
            <dd>
              {formatNumber(point.bucket.minimum, 2)} – {formatNumber(point.bucket.maximum, 2)}
            </dd>
          </div>
          <div className={styles.tooltipRow}>
            <dt>Samples</dt>
            <dd>{formatNumber(point.bucket.samples)}</dd>
          </div>
        </dl>
      ) : (
        <p className={styles.tooltipEmpty}>No samples</p>
      )}
    </>
  );
}

function Chart({ points, range }: Readonly<{ points: EnergyTrendPoint[]; range: TimeRange }>) {
  if (points.every((point) => point.value === null)) {
    return (
      <EmptyState
        title="No energy samples in the last 24 hours"
        description="WeakApp may have been unavailable in this period."
      />
    );
  }

  const lengthMs = bucketLengthMs(ENERGY_TREND_BUCKET);
  const gapCount = gapRuns(points, lengthMs).length;
  const to = range.to.getTime();

  return (
    <>
      <div className={styles.legend}>
        <span className={styles.legendItem}>
          <span aria-hidden="true" className={styles.lineSwatch} />
          {'Average'}
        </span>
        {gapCount > 0 ? (
          <span className={styles.legendItem}>
            <span aria-hidden="true" className={styles.gapSwatch} />
            {'No samples'}
          </span>
        ) : null}
      </div>
      <TimeSeriesChart
        points={points}
        bucketLengthMs={lengthMs}
        domain={[bucketStartOf(range.from, ENERGY_TREND_BUCKET).getTime(), to]}
        ticks={[...localHourTicks(range, TICK_STEP_HOURS).map((tick) => tick.getTime()), to]}
        formatTick={formatTick}
        formatValue={formatValue}
        renderTooltip={(point) => <EnergyTooltip point={point} />}
        label={describeEnergyTrend(points)}
        height={CHART_HEIGHT}
      />
    </>
  );
}

interface EnergyTrendCardProps {
  trend: WidgetQuery<EnergyTrendPoint[]>;
  range: TimeRange;
}

export function EnergyTrendCard({ trend, range }: Readonly<EnergyTrendCardProps>) {
  return (
    <Card
      title="Energy, last 24 hours"
      description={`Average of all locations per ${timeBucketLabel(ENERGY_TREND_BUCKET)}`}
      busy={trend.status === 'loading'}
      className={styles.card}
    >
      {trend.status === 'loading' ? <Skeleton height={`${String(CHART_HEIGHT)}px`} /> : null}
      {trend.status === 'error' ? (
        <WidgetError
          message="This chart couldn't be loaded."
          onRetry={trend.retry}
          className={styles.error}
        />
      ) : null}
      {trend.status === 'ready' ? <Chart points={trend.data} range={range} /> : null}
    </Card>
  );
}
