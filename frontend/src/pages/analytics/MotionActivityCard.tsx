import { Card } from '../../components/Card';
import { ChartLegend, type LegendItem } from '../../components/charts/ChartLegend';
import { Heatmap, type HeatmapRow } from '../../components/charts/Heatmap';
import { EmptyState } from '../../components/states/EmptyState';
import { Skeleton } from '../../components/states/Skeleton';
import { WidgetError } from '../../components/states/WidgetError';
import { timeAxis } from '../../domain/axisTicks';
import { ACTIVITY_CLASS_COUNT, activityClass, activityClassLabel } from '../../domain/activity';
import { formatBucketRange, formatNumber, formatPercent } from '../../domain/formatting';
import { meterLocationLabel } from '../../domain/meters';
import {
  bucketGrid,
  bucketLengthMs,
  bucketStartOf,
  timeBucketLabel,
} from '../../domain/timeBuckets';
import type { TimeBucket } from '../../graphql/generated/enums';
import type { WidgetQuery } from '../../graphql/widgetQuery';
import { describeRange } from './chartAxis';
import type { ActivityRow, MotionActivity } from './motionActivity';

const legend: LegendItem[] = [
  ...Array.from({ length: ACTIVITY_CLASS_COUNT }, (_, index) => ({
    label: activityClassLabel(index),
    swatch: 'square' as const,
    color: `var(--color-heat-${String(index + 1)})`,
  })),
  { label: 'No samples', swatch: 'gap' },
];

function overall(row: ActivityRow): string {
  return `${meterLocationLabel(row.location)} ${row.overallRatio === null ? 'no samples' : formatPercent(row.overallRatio)}`;
}

function Activity({ activity }: Readonly<{ activity: MotionActivity }>) {
  const { range, bucket, rows } = activity;

  if (rows.every((row) => row.overallRatio === null)) {
    return (
      <EmptyState
        title="No motion samples in this range"
        description="WeakApp may have been unavailable in this period."
      />
    );
  }

  const grid = bucketGrid(range, bucket);
  const firstMs = bucketStartOf(range.from, bucket).getTime();
  const spanMs = grid.length * bucketLengthMs(bucket);
  const heatmapRows: HeatmapRow[] = rows.map((row) => ({
    key: row.location,
    label: meterLocationLabel(row.location),
    cells: row.cells.map(({ start, stats }) => {
      const prefix = `${meterLocationLabel(row.location)} · ${formatBucketRange(start, bucket)}`;
      return {
        key: String(start.getTime()),
        level: stats ? activityClass(stats.activityRatio) : null,
        title: stats
          ? `${prefix} · ${formatPercent(stats.activityRatio)} of ${formatNumber(stats.samples)} samples with motion`
          : `${prefix} · No samples`,
      };
    }),
  }));

  return (
    <>
      <Heatmap
        rows={heatmapRows}
        ticks={timeAxis(range, bucket).map((tick) => ({
          position: (tick.time - firstMs) / spanMs,
          label: tick.label,
        }))}
        label={`Share of samples that detected motion per ${timeBucketLabel(bucket)} ${describeRange(range)}. Over the whole range: ${rows.map(overall).join(', ')}.`}
      />
      <ChartLegend items={legend} />
    </>
  );
}

interface MotionActivityCardProps {
  activity: WidgetQuery<MotionActivity>;
  /** The applied bucket. */
  bucket: TimeBucket;
}

export function MotionActivityCard({ activity, bucket }: Readonly<MotionActivityCardProps>) {
  return (
    <Card
      title="Motion activity"
      description={`Share of samples that detected motion, per location and ${timeBucketLabel(bucket)}`}
      busy={activity.status === 'loading'}
      refreshing={activity.status === 'ready' && activity.refreshing}
    >
      {activity.status === 'loading' ? <Skeleton height="200px" /> : null}
      {activity.status === 'error' ? (
        <WidgetError message="This chart couldn't be loaded." onRetry={activity.retry} />
      ) : null}
      {activity.status === 'ready' ? <Activity activity={activity.data} /> : null}
    </Card>
  );
}
