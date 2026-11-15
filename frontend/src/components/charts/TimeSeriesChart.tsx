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
  points: readonly TPoint[];
  bucketLengthMs: number;
  domain: [number, number];
  ticks: readonly number[];
  formatTick: (time: number) => string;
  formatValue: (value: number) => string;
  renderTooltip: (point: TPoint) => ReactNode;
  label: string;
  height?: number;
}

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
