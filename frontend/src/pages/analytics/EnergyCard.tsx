import { useState } from 'react';

import { Card } from '../../components/Card';
import { ChartLegend, type LegendItem } from '../../components/charts/ChartLegend';
import { ChartTooltip, type ChartTooltipRow } from '../../components/charts/ChartTooltip';
import { gapRuns } from '../../components/charts/gapRuns';
import { type ChartLine, TimeSeriesChart } from '../../components/charts/TimeSeriesChart';
import { SegmentedControl } from '../../components/SegmentedControl';
import { EmptyState } from '../../components/states/EmptyState';
import { Skeleton } from '../../components/states/Skeleton';
import { WidgetError } from '../../components/states/WidgetError';
import { formatBucketRange, formatNumber } from '../../domain/formatting';
import { meterLocationLabel, meterLocations } from '../../domain/meters';
import { bucketLengthMs, timeBucketLabel } from '../../domain/timeBuckets';
import type { MeterLocation, TimeBucket } from '../../graphql/generated/enums';
import type { WidgetQuery } from '../../graphql/widgetQuery';
import { chartAxis, describeRange } from './chartAxis';
import styles from './EnergyCard.module.css';
import type { EnergyPoint, EnergySeries, EnergyStats } from './energySeries';
import { locationColor } from './locationColors';

export const ENERGY_CHART_HEIGHT = 300;

type EnergyView = 'combined' | 'byLocation';

const viewOptions = [
  { value: 'combined', label: 'Combined' },
  { value: 'byLocation', label: 'By location' },
] as const;

function formatEnergy(value: number): string {
  return formatNumber(value, 2);
}

function formatAxisValue(value: number): string {
  return formatNumber(value);
}

function linesSubject(location: MeterLocation | null, view: EnergyView): string {
  if (location) {
    return meterLocationLabel(location);
  }
  return view === 'byLocation' ? 'each location' : 'all locations';
}

function describeLines(location: MeterLocation | null, view: EnergyView, bucket: TimeBucket) {
  return `Average of ${linesSubject(location, view)} per ${timeBucketLabel(bucket)}`;
}

function statsRows(stats: EnergyStats): ChartTooltipRow[] {
  return [
    { label: 'Average', value: formatEnergy(stats.average) },
    {
      label: 'Min – max',
      value: `${formatEnergy(stats.minimum)} – ${formatEnergy(stats.maximum)}`,
    },
    { label: 'Samples', value: formatNumber(stats.samples) },
  ];
}

function locationRows(point: EnergyPoint): ChartTooltipRow[] {
  return meterLocations.map((location) => {
    const stats = point.byLocation[location];
    return {
      label: meterLocationLabel(location),
      value: stats ? formatEnergy(stats.average) : '—',
      color: locationColor(location),
    };
  });
}

function Chart({ series, view }: Readonly<{ series: EnergySeries; view: EnergyView }>) {
  const { points, range, bucket, location } = series;
  const combined = points.map((point) => ({
    time: point.time,
    value: point.combined?.average ?? null,
  }));

  if (combined.every((point) => point.value === null)) {
    return (
      <EmptyState
        title="No energy samples in this range"
        description="WeakApp may have been unavailable in this period."
      />
    );
  }

  const byLocation = location === null && view === 'byLocation';
  const lines: ChartLine<EnergyPoint>[] = byLocation
    ? meterLocations.map((each) => ({
        key: each,
        color: locationColor(each),
        value: (point) => point.byLocation[each]?.average ?? null,
      }))
    : [
        {
          key: 'average',
          color: 'var(--color-chart)',
          value: (point) => point.combined?.average ?? null,
        },
      ];

  const gapCount = gapRuns(combined, bucketLengthMs(bucket)).length;
  const legend: LegendItem[] = byLocation
    ? meterLocations.map((each) => ({
        label: meterLocationLabel(each),
        swatch: 'square',
        color: locationColor(each),
      }))
    : [{ label: location ? meterLocationLabel(location) : 'Average', swatch: 'line' }];
  if (gapCount > 0) {
    legend.push({ label: 'No samples', swatch: 'gap' });
  }

  const gaps =
    gapCount > 0
      ? ` No samples in ${String(gapCount)} ${gapCount === 1 ? 'period' : 'periods'}.`
      : '';

  return (
    <>
      <ChartLegend items={legend} />
      <TimeSeriesChart
        points={points}
        lines={lines}
        bucketLengthMs={bucketLengthMs(bucket)}
        {...chartAxis(range, bucket)}
        formatValue={formatAxisValue}
        renderTooltip={(point) => (
          <ChartTooltip
            title={formatBucketRange(new Date(point.time), bucket)}
            rows={point.combined && (byLocation ? locationRows(point) : statsRows(point.combined))}
          />
        )}
        label={`Average energy of ${linesSubject(location, view)} per ${timeBucketLabel(bucket)} ${describeRange(range)}.${gaps}`}
        height={ENERGY_CHART_HEIGHT}
      />
    </>
  );
}

interface EnergyCardProps {
  energy: WidgetQuery<EnergySeries>;
  /** The applied filters; a refreshing chart still shows the series it was loaded for. */
  location: MeterLocation | null;
  bucket: TimeBucket;
}

export function EnergyCard({ energy, location, bucket }: Readonly<EnergyCardProps>) {
  const [view, setView] = useState<EnergyView>('byLocation');

  return (
    <Card
      title="Energy"
      description={describeLines(location, view, bucket)}
      busy={energy.status === 'loading'}
      refreshing={energy.status === 'ready' && energy.refreshing}
      actions={
        location === null ? (
          <SegmentedControl
            label="Energy view"
            options={viewOptions}
            value={view}
            onChange={setView}
          />
        ) : null
      }
    >
      {energy.status === 'loading' ? (
        <Skeleton height={`${String(ENERGY_CHART_HEIGHT)}px`} />
      ) : null}
      {energy.status === 'error' ? (
        <WidgetError
          message="This chart couldn't be loaded."
          onRetry={energy.retry}
          className={styles.error}
        />
      ) : null}
      {energy.status === 'ready' ? <Chart series={energy.data} view={view} /> : null}
    </Card>
  );
}
