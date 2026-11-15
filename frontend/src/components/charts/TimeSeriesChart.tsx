import type { ReactNode } from 'react';
import {
  CartesianGrid,
  Line,
  LineChart,
  ReferenceArea,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';

import { gapRuns, type TimeSeriesPoint } from './gapRuns';
import styles from './TimeSeriesChart.module.css';

const axisTick = { fill: 'var(--color-text-muted)', fontSize: 11 };

interface TimeSeriesChartProps<TPoint extends TimeSeriesPoint> {
  /** One point per bucket, oldest first; `null` values are gaps. */
  points: readonly TPoint[];
  bucketLengthMs: number;
  /** The x range in epoch ms: from the first bucket's start to the end of the time range. */
  domain: [number, number];
  /** The x ticks in epoch ms. */
  ticks: readonly number[];
  formatTick: (time: number) => string;
  formatValue: (value: number) => string;
  /** The tooltip's content for the hovered bucket, gaps included. */
  renderTooltip: (point: TPoint) => ReactNode;
  /** Describes the chart for screen readers. */
  label: string;
  height?: number;
}

/**
 * A line over time buckets. The line breaks at buckets without samples, which are shaded as
 * "No samples". The chart is one image for screen readers, so its `label` sums it up.
 */
export function TimeSeriesChart<TPoint extends TimeSeriesPoint>({
  points,
  bucketLengthMs,
  domain,
  ticks,
  formatTick,
  formatValue,
  renderTooltip,
  label,
  height = 270,
}: Readonly<TimeSeriesChartProps<TPoint>>) {
  return (
    <div role="img" aria-label={label} className={styles.chart}>
      <ResponsiveContainer width="100%" height={height}>
        <LineChart
          data={points}
          margin={{ top: 8, right: 12, bottom: 0, left: 0 }}
          accessibilityLayer={false}
        >
          <CartesianGrid vertical={false} stroke="var(--color-gridline)" />
          <XAxis
            dataKey="time"
            type="number"
            scale="time"
            domain={domain}
            allowDataOverflow
            ticks={ticks}
            tickFormatter={formatTick}
            interval="preserveStartEnd"
            tick={axisTick}
            tickLine={false}
            axisLine={{ stroke: 'var(--color-border-strong)' }}
          />
          <YAxis
            width={48}
            domain={[0, 'auto']}
            tickFormatter={formatValue}
            tick={axisTick}
            tickLine={false}
            axisLine={false}
          />
          {gapRuns(points, bucketLengthMs).map((gap) => (
            <ReferenceArea
              key={gap.from}
              x1={gap.from}
              x2={gap.to}
              ifOverflow="hidden"
              fill="var(--color-surface-muted)"
              fillOpacity={1}
              stroke="var(--color-border-strong)"
              strokeDasharray="3 3"
            />
          ))}
          <Tooltip
            filterNull={false}
            isAnimationActive={false}
            cursor={{ stroke: 'var(--color-text-secondary)' }}
            content={({ active, label: time }) => {
              const point = active
                ? points.find((candidate) => candidate.time === time)
                : undefined;
              return point ? <div className={styles.tooltip}>{renderTooltip(point)}</div> : null;
            }}
          />
          <Line
            dataKey="value"
            type="linear"
            connectNulls={false}
            dot={false}
            activeDot={{
              r: 5,
              fill: 'var(--color-chart)',
              stroke: 'var(--color-surface)',
              strokeWidth: 2,
            }}
            stroke="var(--color-chart)"
            strokeWidth={2}
            isAnimationActive={false}
          />
        </LineChart>
      </ResponsiveContainer>
    </div>
  );
}
