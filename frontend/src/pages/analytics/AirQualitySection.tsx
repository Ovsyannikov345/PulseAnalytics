import { useId } from 'react';

import { Card } from '../../components/Card';
import { ChartLegend, type LegendItem } from '../../components/charts/ChartLegend';
import { ChartTooltip } from '../../components/charts/ChartTooltip';
import { gapRuns } from '../../components/charts/gapRuns';
import { TimeSeriesChart } from '../../components/charts/TimeSeriesChart';
import { EmptyState } from '../../components/states/EmptyState';
import { Skeleton } from '../../components/states/Skeleton';
import { WidgetError } from '../../components/states/WidgetError';
import { formatBucketRange, formatNumber } from '../../domain/formatting';
import { meterLocationLabel } from '../../domain/meters';
import { bucketLengthMs, timeBucketLabel } from '../../domain/timeBuckets';
import type { MeterLocation } from '../../graphql/generated/enums';
import type { WidgetQuery } from '../../graphql/widgetQuery';
import styles from './AirQualitySection.module.css';
import {
  airQualityMetrics,
  type AirQualityMetric,
  type AirQualitySeries,
  type MetricPoint,
} from './airQualitySeries';
import { chartAxis, describeGaps, describeRange } from './chartAxis';

const CHART_HEIGHT = 200;

const metricDefinitions: Record<
  AirQualityMetric,
  { label: string; unit: string; fractionDigits: number }
> = {
  co2: { label: 'CO₂', unit: 'ppm', fractionDigits: 0 },
  pm25: { label: 'PM2.5', unit: 'µg/m³', fractionDigits: 1 },
  humidity: { label: 'Humidity', unit: '%', fractionDigits: 1 },
};

function formatAxisValue(value: number): string {
  return formatNumber(value);
}

interface MetricCardProps {
  metric: AirQualityMetric;
  series: AirQualitySeries;
  refreshing: boolean;
}

function MetricCard({ metric, series, refreshing }: Readonly<MetricCardProps>) {
  const { label, unit, fractionDigits } = metricDefinitions[metric];
  const { points, average } = series.metrics[metric];
  const { range, bucket } = series;
  const format = (value: number) => `${formatNumber(value, fractionDigits)} ${unit}`;

  const gapCount = gapRuns(
    points.map((point) => ({ time: point.time, value: point.stats?.average ?? null })),
    bucketLengthMs(bucket),
  ).length;
  const legend: LegendItem[] = [
    { label: 'Average', swatch: 'line' },
    { label: 'Min – max', swatch: 'band' },
  ];
  if (gapCount > 0) {
    legend.push({ label: 'No samples', swatch: 'gap' });
  }

  return (
    <Card
      title={label}
      headingLevel={3}
      refreshing={refreshing}
      actions={
        average === null ? null : (
          <span className={styles.average}>
            avg <strong>{formatNumber(average, fractionDigits)}</strong> {unit}
          </span>
        )
      }
    >
      <ChartLegend items={legend} />
      <TimeSeriesChart<MetricPoint>
        points={points}
        lines={[
          {
            key: 'average',
            color: 'var(--color-chart)',
            value: (point) => point.stats?.average ?? null,
          },
        ]}
        band={{
          color: 'var(--color-chart-band)',
          range: (point) => (point.stats ? [point.stats.minimum, point.stats.maximum] : null),
        }}
        bucketLengthMs={bucketLengthMs(bucket)}
        {...chartAxis(range, bucket)}
        formatValue={formatAxisValue}
        renderTooltip={(point) => (
          <ChartTooltip
            title={formatBucketRange(new Date(point.time), bucket)}
            rows={
              point.stats && [
                { label: 'Average', value: format(point.stats.average) },
                {
                  label: 'Min – max',
                  value: `${formatNumber(point.stats.minimum, fractionDigits)} – ${format(point.stats.maximum)}`,
                },
                { label: 'Samples', value: formatNumber(point.stats.samples) },
              ]
            }
          />
        )}
        label={`${label}, ${unit}: average with a minimum to maximum band per ${timeBucketLabel(bucket)} ${describeRange(range)}.${describeGaps(gapCount)}`}
        height={CHART_HEIGHT}
      />
    </Card>
  );
}

interface AirQualitySectionProps {
  airQuality: WidgetQuery<AirQualitySeries>;
  /** The applied location filter. */
  location: MeterLocation | null;
}

/** Small multiples: one chart per air quality metric, all from one operation. */
export function AirQualitySection({ airQuality, location }: Readonly<AirQualitySectionProps>) {
  const headingId = useId();
  const empty =
    airQuality.status === 'ready' &&
    airQualityMetrics.every((metric) => airQuality.data.metrics[metric].average === null);

  return (
    <section
      aria-labelledby={headingId}
      aria-busy={airQuality.status === 'loading'}
      className={styles.section}
    >
      <h2 id={headingId} className={styles.heading}>
        Air quality, {location ? meterLocationLabel(location) : 'all selected locations'}
      </h2>
      {airQuality.status === 'loading' ? (
        <div className={styles.grid}>
          {airQualityMetrics.map((metric) => (
            <Skeleton key={metric} height="300px" />
          ))}
        </div>
      ) : null}
      {airQuality.status === 'error' ? (
        <WidgetError message="These charts couldn't be loaded." onRetry={airQuality.retry} />
      ) : null}
      {empty ? (
        <div className={styles.empty}>
          <EmptyState
            title="No air quality samples in this range"
            description="WeakApp may have been unavailable in this period."
          />
        </div>
      ) : null}
      {airQuality.status === 'ready' && !empty ? (
        <div className={styles.grid}>
          {airQualityMetrics.map((metric) => (
            <MetricCard
              key={metric}
              metric={metric}
              series={airQuality.data}
              refreshing={airQuality.refreshing}
            />
          ))}
        </div>
      ) : null}
    </section>
  );
}
