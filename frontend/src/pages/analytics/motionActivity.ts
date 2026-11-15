import { meterLocations } from '../../domain/meters';
import { bucketGrid, groupBuckets } from '../../domain/timeBuckets';
import type { TimeRange } from '../../domain/timeRanges';
import type { MeterLocation, TimeBucket } from '../../graphql/generated/enums';
import type { AnalyticsMotionActivityQuery } from '../../graphql/generated/graphql';

type MotionRow = AnalyticsMotionActivityQuery['motionStatistics'][number];

export interface ActivityStats {
  samples: number;
  motionSamples: number;
  activityRatio: number;
}

export interface ActivityCell {
  start: Date;
  /** `null` where the location has no samples. */
  stats: ActivityStats | null;
}

export interface ActivityRow {
  location: MeterLocation;
  /** One per bucket of the range. */
  cells: ActivityCell[];
  /** The share of the row's samples with motion; `null` without samples. */
  overallRatio: number | null;
}

export interface MotionActivity {
  range: TimeRange;
  bucket: TimeBucket;
  rows: ActivityRow[];
}

/** One row per selected location (all six without a filter), one cell per bucket. */
export function toMotionActivity(
  rows: readonly MotionRow[],
  range: TimeRange,
  bucket: TimeBucket,
  location: MeterLocation | null,
): MotionActivity {
  const groups = groupBuckets(bucketGrid(range, bucket), rows, (row) => row.bucketStart);

  return {
    range,
    bucket,
    rows: (location ? [location] : meterLocations).map((rowLocation) => {
      const cells = groups.map(({ start, rows: bucketRows }) => {
        const row = bucketRows.find((candidate) => candidate.location === rowLocation);
        return {
          start,
          stats: row
            ? {
                samples: row.samples,
                motionSamples: row.motionSamples,
                activityRatio: row.activityRatio,
              }
            : null,
        };
      });

      let samples = 0;
      let motionSamples = 0;
      for (const { stats } of cells) {
        samples += stats?.samples ?? 0;
        motionSamples += stats?.motionSamples ?? 0;
      }

      return {
        location: rowLocation,
        cells,
        overallRatio: samples > 0 ? motionSamples / samples : null,
      };
    }),
  };
}
