import type { TimePoint } from '../../components/charts/TimeSeriesChart';
import { bucketGrid, fillBuckets } from '../../domain/timeBuckets';
import type { TimeRange } from '../../domain/timeRanges';
import type { MeterLocation, TimeBucket } from '../../graphql/generated/enums';
import type { AnalyticsAirQualityQuery } from '../../graphql/generated/graphql';

type AirQualityRow = AnalyticsAirQualityQuery['airQualityStatistics'][number];

export type AirQualityMetric = 'co2' | 'pm25' | 'humidity';

export const airQualityMetrics: readonly AirQualityMetric[] = ['co2', 'pm25', 'humidity'];

export interface MetricPoint extends TimePoint {
  /** `null` for a bucket without samples (a gap). */
  stats: { average: number; minimum: number; maximum: number; samples: number } | null;
}

export interface MetricSeries {
  points: MetricPoint[];
  /** Over the whole range, weighted by samples; `null` without samples. */
  average: number | null;
}

export interface AirQualitySeries {
  range: TimeRange;
  bucket: TimeBucket;
  location: MeterLocation | null;
  metrics: Record<AirQualityMetric, MetricSeries>;
}

function toMetricSeries(
  slots: readonly { start: Date; value: AirQualityRow | null }[],
  metric: AirQualityMetric,
): MetricSeries {
  const points = slots.map(({ start, value: row }) => ({
    time: start.getTime(),
    stats: row
      ? {
          average: row[metric].average,
          minimum: row[metric].minimum,
          maximum: row[metric].maximum,
          samples: row.samples,
        }
      : null,
  }));

  let weightedSum = 0;
  let samples = 0;
  for (const { stats } of points) {
    if (stats) {
      weightedSum += stats.average * stats.samples;
      samples += stats.samples;
    }
  }

  return { points, average: samples > 0 ? weightedSum / samples : null };
}

export function toAirQualitySeries(
  rows: readonly AirQualityRow[],
  range: TimeRange,
  bucket: TimeBucket,
  location: MeterLocation | null,
): AirQualitySeries {
  const slots = fillBuckets(bucketGrid(range, bucket), rows, (row) => row.bucketStart);

  return {
    range,
    bucket,
    location,
    metrics: {
      co2: toMetricSeries(slots, 'co2'),
      pm25: toMetricSeries(slots, 'pm25'),
      humidity: toMetricSeries(slots, 'humidity'),
    },
  };
}
